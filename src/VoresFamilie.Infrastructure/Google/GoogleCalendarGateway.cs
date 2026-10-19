using System.Net;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using GoogleCalendarService = Google.Apis.Calendar.v3.CalendarService;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Http;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

public sealed class GoogleCalendarGateway(GoogleAuthService auth) : IGoogleCalendarGateway
{
    private static readonly TimeZoneInfo Local = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public Task<IReadOnlyList<GoogleCalendarInfo>> ListCalendarsAsync(string userId, CancellationToken ct = default) =>
        CallAsync<IReadOnlyList<GoogleCalendarInfo>>(userId, async svc =>
        {
            var result = new List<GoogleCalendarInfo>();
            string? page = null;
            do
            {
                var req = svc.CalendarList.List();
                req.PageToken = page;
                var list = await req.ExecuteAsync(ct);
                result.AddRange((list.Items ?? []).Select(c => new GoogleCalendarInfo(
                    c.Id, c.SummaryOverride ?? c.Summary ?? c.Id, c.BackgroundColor, c.AccessRole is "owner" or "writer")));
                page = list.NextPageToken;
            } while (page is not null);
            return result;
        }, ct);

    public Task<GoogleEventPage> ListEventsAsync(string userId, string googleCalendarId, string? syncToken,
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken ct = default) =>
        CallAsync(userId, async svc =>
        {
            var changes = new List<GoogleEventChange>();
            string? page = null, next = null;
            do
            {
                var req = svc.Events.List(googleCalendarId);
                req.SingleEvents = true;          // recurring events arrive as individual occurrences
                req.MaxResults = 250;
                req.PageToken = page;
                if (syncToken is not null)
                {
                    req.SyncToken = syncToken;
                    req.ShowDeleted = true;       // required to learn about deletions
                }
                else
                {
                    req.TimeMinDateTimeOffset = windowStart;
                    req.TimeMaxDateTimeOffset = windowEnd;
                }

                Events events;
                try { events = await req.ExecuteAsync(ct); }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Gone)
                {
                    throw new GoogleSyncTokenExpiredException();
                }

                foreach (var e in events.Items ?? [])
                    changes.Add(new GoogleEventChange(e.Id, e.Status == "cancelled" ? null : ToData(e)));
                page = events.NextPageToken;
                next = events.NextSyncToken ?? next;
            } while (page is not null);
            return new GoogleEventPage(changes, next);
        }, ct);

    public Task<GoogleEventData> InsertEventAsync(string userId, string googleCalendarId, GoogleEventInput input, CancellationToken ct = default) =>
        CallAsync(userId, async svc => ToData(await svc.Events.Insert(ToEvent(input), googleCalendarId).ExecuteAsync(ct)), ct);

    public Task<GoogleEventData> UpdateEventAsync(string userId, string googleCalendarId, string eventId, string? etag,
        GoogleEventInput input, CancellationToken ct = default) =>
        CallAsync(userId, async svc =>
        {
            // Patch changes only the fields we send, so reminders, attendees etc. set elsewhere are kept.
            var body = ToEvent(input);
            body.ETag = etag;
            var req = svc.Events.Patch(body, googleCalendarId, eventId);
            if (etag is not null) req.ETagAction = Google.Apis.ETagAction.IfMatch;
            try { return ToData(await req.ExecuteAsync(ct)); }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
            {
                throw new GoogleConflictException();
            }
        }, ct);

    public Task DeleteEventAsync(string userId, string googleCalendarId, string eventId, CancellationToken ct = default) =>
        CallAsync(userId, async svc =>
        {
            try { await svc.Events.Delete(googleCalendarId, eventId).ExecuteAsync(ct); }
            catch (GoogleApiException ex) when (ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) { /* already gone */ }
            return 0;
        }, ct);

    private async Task<T> CallAsync<T>(string userId, Func<GoogleCalendarService, Task<T>> call, CancellationToken ct)
    {
        using var svc = await auth.CreateServiceAsync(userId, ct);
        try { return await call(svc); }
        catch (TokenResponseException ex) when (ex.Error?.Error == "invalid_grant")
        {
            await auth.ForgetAsync(userId);
            throw new DomainException("Google-forbindelsen er udløbet. Forbind Google Kalender igen.");
        }
    }

    // ---- mapping ----

    private static GoogleEventData ToData(Event e)
    {
        var allDay = e.Start?.DateTimeDateTimeOffset is null && e.Start?.Date is not null;
        var start = allDay ? DateOnlyToLocal(e.Start!.Date) : e.Start?.DateTimeDateTimeOffset ?? DateTimeOffset.MinValue;
        var end = allDay ? DateOnlyToLocal(e.End?.Date ?? e.Start!.Date) : e.End?.DateTimeDateTimeOffset ?? start;
        return new GoogleEventData(e.Id, e.ETag, e.Summary ?? "", e.Description, e.Location, start, end, allDay, e.UpdatedDateTimeOffset);
    }

    private static Event ToEvent(GoogleEventInput i)
    {
        var e = new Event { Summary = i.Title, Description = i.Description, Location = i.Location };
        if (i.AllDay)
        {
            var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(i.Start, Local).DateTime);
            var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(i.End, Local).DateTime);
            if (end <= start) end = start.AddDays(1); // Google's all-day end date is exclusive
            e.Start = new EventDateTime { Date = start.ToString("yyyy-MM-dd") };
            e.End = new EventDateTime { Date = end.ToString("yyyy-MM-dd") };
        }
        else
        {
            e.Start = new EventDateTime { DateTimeDateTimeOffset = i.Start, TimeZone = Local.Id };
            e.End = new EventDateTime { DateTimeDateTimeOffset = i.End, TimeZone = Local.Id };
        }
        return e;
    }

    private static DateTimeOffset DateOnlyToLocal(string date)
    {
        var d = DateTime.SpecifyKind(DateTime.ParseExact(date, "yyyy-MM-dd", null), DateTimeKind.Unspecified);
        return new DateTimeOffset(d, Local.GetUtcOffset(d));
    }
}

