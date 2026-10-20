using VoresFamilie.Domain;

namespace VoresFamilie.Application;

/// <summary>Pulls changes from Google into the local cache using incremental sync (sync tokens).</summary>
public sealed class CalendarSyncService(ICalendarRepository calendars, IGoogleCalendarGateway google, IClock clock)
{
    /// <summary>How far back and forward events are cached on a full sync.</summary>
    public static readonly TimeSpan WindowBack = TimeSpan.FromDays(90);
    public static readonly TimeSpan WindowForward = TimeSpan.FromDays(365);
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

    public async Task SyncAsync(FamilyCalendar calendar, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        GoogleEventPage page;
        try
        {
            page = await google.ListEventsAsync(calendar.ConnectedByUserId, calendar.GoogleCalendarId,
                calendar.SyncToken, now - WindowBack, now + WindowForward, ct);
        }
        catch (GoogleSyncTokenExpiredException)
        {
            // Google forgot our position: throw the cache away and start over.
            calendar.ResetSync();
            await calendars.ClearEventsAsync(calendar.Id, ct);
            page = await google.ListEventsAsync(calendar.ConnectedByUserId, calendar.GoogleCalendarId,
                null, now - WindowBack, now + WindowForward, ct);
        }

        foreach (var change in page.Changes)
        {
            var existing = await calendars.FindEventAsync(calendar.Id, change.Id, ct);
            if (change.Data is null)
            {
                if (existing is not null) await RemoveWithCompletionAsync(calendar, existing, ct);
                continue;
            }
            await UpsertAsync(calendar, existing, change.Data, ct);
        }

        calendar.RecordSync(page.NextSyncToken, now);
        await calendars.SaveChangesAsync(ct);
    }

    public async Task SyncIfStaleAsync(IEnumerable<FamilyCalendar> list, CancellationToken ct = default)
    {
        foreach (var c in list.Where(c => c.IsStale(clock.UtcNow, MaxAge)))
        {
            try { await SyncAsync(c, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* show cached data; the background sync retries */ }
        }
    }

    /// <summary>Syncs every calendar of every family. Used by the background service.</summary>
    public async Task<SyncResultDto> SyncAllAsync(CancellationToken ct = default)
    {
        var all = await calendars.GetAllCalendarsAsync(ct);
        var failed = 0;
        foreach (var c in all)
        {
            try { await SyncAsync(c, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { failed++; }
        }
        return new SyncResultDto(all.Count, failed);
    }

    internal async Task<CalendarEvent> StoreAsync(FamilyCalendar calendar, GoogleEventData data, CancellationToken ct)
    {
        var existing = await calendars.FindEventAsync(calendar.Id, data.Id, ct);
        return await UpsertAsync(calendar, existing, data, ct);
    }

    private async Task<CalendarEvent> UpsertAsync(FamilyCalendar calendar, CalendarEvent? existing, GoogleEventData d, CancellationToken ct)
    {
        if (existing is null)
        {
            var created = CalendarEvent.Create(calendar.Id, d.Id, d.ETag, d.Title, d.Description, d.Location, d.Start, d.End, d.AllDay, d.Updated);
            await calendars.AddEventAsync(created, ct);
            return created;
        }
        existing.Apply(d.ETag, d.Title, d.Description, d.Location, d.Start, d.End, d.AllDay, d.Updated);
        return existing;
    }

    internal async Task RemoveWithCompletionAsync(FamilyCalendar calendar, CalendarEvent e, CancellationToken ct)
    {
        var completion = await calendars.FindCompletionAsync(calendar.Id, e.GoogleEventId, ct);
        if (completion is not null) await calendars.RemoveCompletionAsync(completion, ct);
        await calendars.RemoveEventAsync(e, ct);
    }
}

