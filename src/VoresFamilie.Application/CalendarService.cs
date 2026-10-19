using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class CalendarService(IFamilyRepository families, ICalendarRepository calendars,
    IGoogleCalendarGateway google, IGoogleAuthService googleAuth, CalendarSyncService sync)
{
    // ---- Google connection ----

    public async Task<GoogleStatusDto> GetGoogleStatusAsync(string userId, CancellationToken ct = default) =>
        new(googleAuth.IsConfigured, googleAuth.IsConfigured && await googleAuth.IsConnectedAsync(userId, ct));

    public async Task<IReadOnlyList<GoogleCalendarDto>> ListGoogleCalendarsAsync(string userId, CancellationToken ct = default)
    {
        var (family, _) = await LoadAsync(userId, adminOnly: false, ct); // everyone lists their own Google account
        var added = (await calendars.GetCalendarsAsync(family.Id, ct)).Select(c => c.GoogleCalendarId).ToHashSet();
        return (await RequireConnectedAsync(userId, () => google.ListCalendarsAsync(userId, ct)))
            .Select(c => new GoogleCalendarDto(c.Id, c.Name, c.Color, added.Contains(c.Id)))
            .ToList();
    }

    public Task DisconnectGoogleAsync(string userId, CancellationToken ct = default) => googleAuth.DisconnectAsync(userId, ct);

    // ---- Calendars ----

    public async Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, adminOnly: false, ct);
        var all = await calendars.GetCalendarsAsync(family.Id, ct);
        return all.Where(c => CalendarRules.CanView(me, c)).Select(c => ToDto(family, me, c)).ToList();
    }

    public async Task<CalendarDto> AddCalendarAsync(string userId, AddCalendarRequest request, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, adminOnly: false, ct);
        // Admin adds family and homework calendars (and anyone's calendar); members add only their own personal calendar,
        // read from their own Google account.
        var ownPersonal = request.Kind == CalendarKind.Personal && (request.OwnerMemberId ?? me.Id) == me.Id;
        if (me.Role != Role.Admin && !ownPersonal) throw new ForbiddenException("Kun admin kan gøre dette.");
        var owner = request.Kind == CalendarKind.Family ? null : request.OwnerMemberId ?? (ownPersonal ? me.Id : null);
        var available = await RequireConnectedAsync(userId, () => google.ListCalendarsAsync(userId, ct));
        var info = available.FirstOrDefault(c => c.Id == request.GoogleCalendarId)
            ?? throw new DomainException("Kalenderen findes ikke på din Google-konto.");
        var existing = await calendars.GetCalendarsAsync(family.Id, ct);
        if (existing.Any(c => c.GoogleCalendarId == request.GoogleCalendarId))
            throw new DomainException("Kalenderen er allerede tilføjet.");
        if (owner is { } ownerId && family.Members.All(m => m.Id != ownerId))
            throw new DomainException("Ejeren er ikke medlem af familien.");

        var name = string.IsNullOrWhiteSpace(request.Name) ? info.Name : request.Name;
        var calendar = FamilyCalendar.Create(family.Id, name, request.Kind, owner,
            request.GoogleCalendarId, userId, request.Color ?? info.Color);
        await calendars.AddCalendarAsync(calendar, ct);
        await calendars.SaveChangesAsync(ct);
        await sync.SyncIfStaleAsync([calendar], ct);
        return ToDto(family, me, calendar);
    }

    public async Task RemoveCalendarAsync(string userId, Guid calendarId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, adminOnly: false, ct);
        var calendar = await GetOwnCalendarAsync(family, calendarId, ct);
        var ownPersonal = calendar.Kind == CalendarKind.Personal && calendar.OwnerMemberId == me.Id && calendar.ConnectedByUserId == userId;
        if (me.Role != Role.Admin && !ownPersonal) throw new ForbiddenException("Kun admin kan gøre dette.");
        await calendars.RemoveCalendarAsync(calendar, ct);
        await calendars.SaveChangesAsync(ct);
    }

    // ---- Events ----

    public async Task<IReadOnlyList<EventDto>> GetEventsAsync(string userId, DateTimeOffset from, DateTimeOffset to,
        IReadOnlyCollection<Guid>? calendarIds, CancellationToken ct = default)
    {
        if (to < from) throw new DomainException("Slut kan ikke ligge før start.");
        var (family, me) = await LoadAsync(userId, adminOnly: false, ct);
        var visible = (await calendars.GetCalendarsAsync(family.Id, ct))
            .Where(c => CalendarRules.CanView(me, c) && (calendarIds is null || calendarIds.Contains(c.Id)))
            .ToList();
        await sync.SyncIfStaleAsync(visible, ct);
        var events = await calendars.GetEventsAsync(visible.Select(c => c.Id).ToList(), from, to, ct);
        return events.OrderBy(e => e.Start).Select(ToDto).ToList();
    }

    public async Task<EventDto> CreateEventAsync(string userId, Guid calendarId, SaveEventRequest request, CancellationToken ct = default)
    {
        var (calendar, _) = await LoadEditableAsync(userId, calendarId, ct);
        Validate(request);
        var data = await RequireConnectedAsync(calendar.ConnectedByUserId,
            () => google.InsertEventAsync(calendar.ConnectedByUserId, calendar.GoogleCalendarId, ToInput(request), ct));
        var stored = await sync.StoreAsync(calendar, data, ct);
        await calendars.SaveChangesAsync(ct);
        return ToDto(stored);
    }

    public async Task<EventDto> UpdateEventAsync(string userId, Guid calendarId, string eventId, SaveEventRequest request, CancellationToken ct = default)
    {
        var (calendar, _) = await LoadEditableAsync(userId, calendarId, ct);
        Validate(request);
        var cached = await calendars.FindEventAsync(calendarId, eventId, ct) ?? throw new NotFoundException("Begivenheden findes ikke.");
        try
        {
            var data = await RequireConnectedAsync(calendar.ConnectedByUserId,
                () => google.UpdateEventAsync(calendar.ConnectedByUserId, calendar.GoogleCalendarId, eventId, cached.ETag, ToInput(request), ct));
            var stored = await sync.StoreAsync(calendar, data, ct);
            await calendars.SaveChangesAsync(ct);
            return ToDto(stored);
        }
        catch (GoogleConflictException ex)
        {
            // Someone changed it in Google meanwhile: refresh the cache so the user sees the current version.
            await sync.SyncIfStaleAsync([calendar], ct);
            throw new ConflictException(ex.Message);
        }
    }

    public async Task DeleteEventAsync(string userId, Guid calendarId, string eventId, CancellationToken ct = default)
    {
        var (calendar, _) = await LoadEditableAsync(userId, calendarId, ct);
        var cached = await calendars.FindEventAsync(calendarId, eventId, ct) ?? throw new NotFoundException("Begivenheden findes ikke.");
        await RequireConnectedAsync(calendar.ConnectedByUserId, async () =>
        {
            await google.DeleteEventAsync(calendar.ConnectedByUserId, calendar.GoogleCalendarId, eventId, ct);
            return 0;
        });
        await sync.RemoveWithCompletionAsync(calendar, cached, ct);
        await calendars.SaveChangesAsync(ct);
    }

    public async Task<SyncResultDto> SyncNowAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, adminOnly: false, ct);
        var visible = (await calendars.GetCalendarsAsync(family.Id, ct)).Where(c => CalendarRules.CanView(me, c)).ToList();
        var failed = 0;
        foreach (var c in visible)
        {
            try { await sync.SyncAsync(c, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { failed++; }
        }
        return new SyncResultDto(visible.Count, failed);
    }

    // ---- helpers ----

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, bool adminOnly, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        var me = family.FindMemberByUser(userId)!;
        if (adminOnly && me.Role != Role.Admin) throw new ForbiddenException("Kun admin kan gøre dette.");
        return (family, me);
    }

    private async Task<FamilyCalendar> GetOwnCalendarAsync(Family family, Guid calendarId, CancellationToken ct)
    {
        var calendar = await calendars.GetCalendarAsync(calendarId, ct);
        if (calendar is null || calendar.FamilyId != family.Id) throw new NotFoundException("Kalenderen findes ikke.");
        return calendar;
    }

    private async Task<(FamilyCalendar, FamilyMember)> LoadEditableAsync(string userId, Guid calendarId, CancellationToken ct)
    {
        var (family, me) = await LoadAsync(userId, adminOnly: false, ct);
        var calendar = await GetOwnCalendarAsync(family, calendarId, ct);
        if (!CalendarRules.CanView(me, calendar)) throw new NotFoundException("Kalenderen findes ikke.");
        if (!CalendarRules.CanEdit(me, calendar)) throw new ForbiddenException("Du må ikke redigere denne kalender.");
        return (calendar, me);
    }

    /// <summary>Turns "no Google token" failures into a clear message.</summary>
    private async Task<T> RequireConnectedAsync<T>(string userId, Func<Task<T>> call)
    {
        if (!googleAuth.IsConfigured) throw new DomainException("Google Kalender er ikke sat op endnu.");
        if (!await googleAuth.IsConnectedAsync(userId)) throw new DomainException("Google-kontoen er ikke forbundet. Forbind den først.");
        return await call();
    }

    private static void Validate(SaveEventRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) throw new DomainException("Begivenheden skal have en titel.");
        if (r.End < r.Start) throw new DomainException("Slut kan ikke ligge før start.");
    }

    private static GoogleEventInput ToInput(SaveEventRequest r) =>
        new(r.Title.Trim(), r.Description, r.Location, r.Start, r.End, r.AllDay);

    private static CalendarDto ToDto(Family family, FamilyMember me, FamilyCalendar c) =>
        new(c.Id, c.Name, c.Kind, c.OwnerMemberId, family.Members.FirstOrDefault(m => m.Id == c.OwnerMemberId)?.DisplayName,
            c.Color, c.LastSyncedAt, CalendarRules.CanEdit(me, c));

    internal static EventDto ToDto(CalendarEvent e) =>
        new(e.GoogleEventId, e.CalendarId, e.Title, e.Description, e.Location, e.Start, e.End, e.AllDay,
            e.IsHomework, HomeworkParser.Parse(e.Description).Subject);
}

