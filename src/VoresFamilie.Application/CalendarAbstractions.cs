using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed record GoogleCalendarInfo(string Id, string Name, string? Color, bool CanWrite);

public sealed record GoogleEventData(string Id, string? ETag, string Title, string? Description, string? Location,
    DateTimeOffset Start, DateTimeOffset End, bool AllDay, DateTimeOffset? Updated);

public sealed record GoogleEventInput(string Title, string? Description, string? Location,
    DateTimeOffset Start, DateTimeOffset End, bool AllDay);

/// <summary>One change from an incremental sync: a deleted event (Data is null) or a new/changed one.</summary>
public sealed record GoogleEventChange(string Id, GoogleEventData? Data);

public sealed record GoogleEventPage(IReadOnlyList<GoogleEventChange> Changes, string? NextSyncToken);

/// <summary>Google rejected the sync token (HTTP 410). A full sync is needed.</summary>
public class GoogleSyncTokenExpiredException() : Exception("Sync token expired.");

/// <summary>The event changed in Google after we read it (HTTP 412).</summary>
public class GoogleConflictException() : Exception("Begivenheden er ændret i Google Kalender. Prøv igen.");

/// <summary>The only place that talks to Google Calendar. Calls are made on behalf of an identity user.</summary>
public interface IGoogleCalendarGateway
{
    Task<IReadOnlyList<GoogleCalendarInfo>> ListCalendarsAsync(string userId, CancellationToken ct = default);

    /// <summary>With a sync token: only changes since then. Without: all events in the window.</summary>
    Task<GoogleEventPage> ListEventsAsync(string userId, string googleCalendarId, string? syncToken,
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken ct = default);

    Task<GoogleEventData> InsertEventAsync(string userId, string googleCalendarId, GoogleEventInput input, CancellationToken ct = default);

    /// <exception cref="GoogleConflictException">The ETag no longer matches.</exception>
    Task<GoogleEventData> UpdateEventAsync(string userId, string googleCalendarId, string eventId, string? etag,
        GoogleEventInput input, CancellationToken ct = default);

    Task DeleteEventAsync(string userId, string googleCalendarId, string eventId, CancellationToken ct = default);
}

/// <summary>Connecting a member's Google account (OAuth authorization code flow). Tokens are stored encrypted.</summary>
public interface IGoogleAuthService
{
    /// <summary>False until a Google client id/secret are configured.</summary>
    bool IsConfigured { get; }

    Task<bool> IsConnectedAsync(string userId, CancellationToken ct = default);

    string BuildAuthorizationUrl(string state, string redirectUri);

    Task ConnectAsync(string userId, string code, string redirectUri, CancellationToken ct = default);

    Task DisconnectAsync(string userId, CancellationToken ct = default);
}

public interface ICalendarRepository
{
    Task<IReadOnlyList<FamilyCalendar>> GetCalendarsAsync(Guid familyId, CancellationToken ct = default);
    Task<IReadOnlyList<FamilyCalendar>> GetAllCalendarsAsync(CancellationToken ct = default);
    Task<FamilyCalendar?> GetCalendarAsync(Guid calendarId, CancellationToken ct = default);
    Task AddCalendarAsync(FamilyCalendar calendar, CancellationToken ct = default);
    /// <summary>Removes the calendar together with its cached events and homework completions.</summary>
    Task RemoveCalendarAsync(FamilyCalendar calendar, CancellationToken ct = default);

    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(IReadOnlyCollection<Guid> calendarIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<CalendarEvent?> FindEventAsync(Guid calendarId, string googleEventId, CancellationToken ct = default);
    Task AddEventAsync(CalendarEvent calendarEvent, CancellationToken ct = default);
    Task RemoveEventAsync(CalendarEvent calendarEvent, CancellationToken ct = default);
    Task ClearEventsAsync(Guid calendarId, CancellationToken ct = default);

    Task<IReadOnlyList<HomeworkCompletion>> GetCompletionsAsync(IReadOnlyCollection<Guid> calendarIds, CancellationToken ct = default);
    Task<HomeworkCompletion?> FindCompletionAsync(Guid calendarId, string googleEventId, CancellationToken ct = default);
    Task AddCompletionAsync(HomeworkCompletion completion, CancellationToken ct = default);
    Task RemoveCompletionAsync(HomeworkCompletion completion, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
