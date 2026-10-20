using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class HomeworkService(IFamilyRepository families, ICalendarRepository calendars, CalendarSyncService sync, IClock clock)
{
    public async Task<IReadOnlyList<HomeworkItemDto>> ListAsync(string userId, DateTimeOffset from, DateTimeOffset to,
        Guid? memberId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var homeworkCalendars = (await calendars.GetCalendarsAsync(family.Id, ct))
            .Where(c => c.Kind == CalendarKind.Homework && CalendarRules.CanView(me, c)
                        && (memberId is null || c.OwnerMemberId == memberId))
            .ToList();
        await sync.SyncIfStaleAsync(homeworkCalendars, ct);

        var ids = homeworkCalendars.Select(c => c.Id).ToList();
        var events = await calendars.GetEventsAsync(ids, from, to, ct);
        var done = (await calendars.GetCompletionsAsync(ids, ct)).ToDictionary(c => (c.CalendarId, c.GoogleEventId));
        var byId = homeworkCalendars.ToDictionary(c => c.Id);

        return events.Where(e => e.IsHomework).OrderBy(e => e.Start).Select(e =>
        {
            var cal = byId[e.CalendarId];
            var child = family.Members.First(m => m.Id == cal.OwnerMemberId);
            done.TryGetValue((e.CalendarId, e.GoogleEventId), out var completion);
            return new HomeworkItemDto(e.CalendarId, e.GoogleEventId, e.Title, e.Description,
                HomeworkParser.Parse(e.Description).Subject, e.Start, e.AllDay, child.Id, child.DisplayName,
                completion is not null, completion?.CompletedAt, CalendarRules.CanCheckHomework(me, cal));
        }).ToList();
    }

    public async Task SetDoneAsync(string userId, Guid calendarId, string eventId, bool done, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var calendar = await calendars.GetCalendarAsync(calendarId, ct);
        if (calendar is null || calendar.FamilyId != family.Id || !CalendarRules.CanView(me, calendar))
            throw new NotFoundException("Kalenderen findes ikke.");
        if (!CalendarRules.CanCheckHomework(me, calendar)) throw new ForbiddenException("Du må ikke afkrydse disse lektier.");
        var ev = await calendars.FindEventAsync(calendarId, eventId, ct);
        if (ev is null || !ev.IsHomework) throw new NotFoundException("Lektien findes ikke.");

        var existing = await calendars.FindCompletionAsync(calendarId, eventId, ct);
        if (done && existing is null)
            await calendars.AddCompletionAsync(HomeworkCompletion.Create(calendarId, eventId, calendar.OwnerMemberId!.Value, clock.UtcNow), ct);
        else if (!done && existing is not null)
            await calendars.RemoveCompletionAsync(existing, ct);
        await calendars.SaveChangesAsync(ct);
    }

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        return (family, family.FindMemberByUser(userId)!);
    }
}
