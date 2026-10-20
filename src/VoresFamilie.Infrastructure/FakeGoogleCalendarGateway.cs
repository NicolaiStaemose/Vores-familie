using VoresFamilie.Application;

namespace VoresFamilie.Infrastructure;

/// <summary>
/// In-memory stand-in for Google Calendar. Used by tests and by `Google:Mode=Fake` so the whole calendar UI
/// can be tried locally without a Google Cloud project. Mimics sync tokens, ETags and conflicts.
/// </summary>
public sealed class FakeGoogleCalendarGateway(IClock clock) : IGoogleCalendarGateway, IGoogleAuthService
{
    private sealed class Item
    {
        public required string Id { get; init; }
        public required string CalendarId { get; init; }
        public GoogleEventInput Input { get; set; } = null!;
        public long Version { get; set; }
        public bool Deleted { get; set; }
    }

    private readonly object _gate = new();
    private readonly List<GoogleCalendarInfo> _calendars = [];
    private readonly List<Item> _items = [];
    private long _version;
    private long _minValidVersion;

    // ---- test/seed helpers ----

    public void AddCalendar(string id, string name, string? color = null)
    {
        lock (_gate) _calendars.Add(new GoogleCalendarInfo(id, name, color, true));
    }

    /// <summary>Creates or changes an event "in Google", as if done from another device.</summary>
    public string Put(string calendarId, string title, DateTimeOffset start, DateTimeOffset end, string? description = null,
        bool allDay = false, string? id = null)
    {
        lock (_gate)
        {
            var input = new GoogleEventInput(title, description, null, start, end, allDay);
            var item = id is null ? null : _items.FirstOrDefault(i => i.CalendarId == calendarId && i.Id == id);
            if (item is null)
            {
                item = new Item { Id = id ?? Guid.NewGuid().ToString("N"), CalendarId = calendarId };
                _items.Add(item);
            }
            item.Input = input;
            item.Deleted = false;
            item.Version = ++_version;
            return item.Id;
        }
    }

    public void Remove(string calendarId, string id)
    {
        lock (_gate)
        {
            var item = _items.First(i => i.CalendarId == calendarId && i.Id == id);
            item.Deleted = true;
            item.Version = ++_version;
        }
    }

    /// <summary>Makes every existing sync token invalid (Google answers 410 Gone).</summary>
    public void ExpireSyncTokens()
    {
        lock (_gate) _minValidVersion = ++_version;
    }

    public int EventCount(string calendarId)
    {
        lock (_gate) return _items.Count(i => i.CalendarId == calendarId && !i.Deleted);
    }

    /// <summary>Demo family: a shared calendar and homework calendars with a typical school week.</summary>
    public void SeedDemo()
    {
        var today = new DateTimeOffset(clock.UtcNow.Date, TimeSpan.Zero);
        AddCalendar("demo-family", "Familien", "#1b6ef3");
        AddCalendar("demo-emma-homework", "Emma lektier", "#e8710a");
        AddCalendar("demo-noah-homework", "Noah lektier", "#0b8043");
        AddCalendar("demo-personal", "Privat", "#8e24aa");
        AddCalendar("demo-emma-schedule", "Emma skema", "#00acc1");
        Put("demo-family", "Aftensmad hos mormor", today.AddDays(1).AddHours(17), today.AddDays(1).AddHours(20));
        Put("demo-family", "Svømning", today.AddDays(2).AddHours(16), today.AddDays(2).AddHours(17));
        Put("demo-family", "Sommerhus-weekend", today.AddDays(5), today.AddDays(7), allDay: true);
        Put("demo-personal", "Tandlæge", today.AddDays(3).AddHours(9), today.AddDays(3).AddHours(10));
        for (var d = 0; d < 5; d++)
        {
            var day = today.AddDays(d);
            Put("demo-emma-schedule", "Dansk", day.AddHours(8), day.AddHours(8.75), "Lokale 12");
            Put("demo-emma-schedule", "Matematik", day.AddHours(9), day.AddHours(9.75), "Lokale 7");
            Put("demo-emma-schedule", "Idræt", day.AddHours(11), day.AddHours(12), "Hallen");
            Put("demo-emma-homework", "Lektie: Dansk", day.AddHours(15), day.AddHours(15.5),
                "Læs side 12-15\n\nBarn: Emma Hansen\nFag: Dansk");
            Put("demo-emma-homework", "99 arter", day.AddHours(8), day.AddHours(8.75),
                "Fælles aktivitet\n\nBarn: Emma Hansen\nFag: Natur/teknologi");
            Put("demo-noah-homework", "LEKTIE: Matematik", day.AddHours(15), day.AddHours(15.5),
                "Opgave 4 og 5\n\nBarn: Noah Hansen\nFag: Matematik");
        }
    }

    // ---- IGoogleCalendarGateway ----

    public Task<IReadOnlyList<GoogleCalendarInfo>> ListCalendarsAsync(string userId, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<GoogleCalendarInfo>>(_calendars.ToList());
    }

    public Task<GoogleEventPage> ListEventsAsync(string userId, string googleCalendarId, string? syncToken,
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var mine = _items.Where(i => i.CalendarId == googleCalendarId);
            IEnumerable<GoogleEventChange> changes;
            if (syncToken is null)
            {
                changes = mine.Where(i => !i.Deleted && i.Input.End > windowStart && i.Input.Start < windowEnd)
                    .Select(i => new GoogleEventChange(i.Id, ToData(i)));
            }
            else
            {
                var since = long.Parse(syncToken.TrimStart('v'));
                if (since < _minValidVersion) throw new GoogleSyncTokenExpiredException();
                changes = mine.Where(i => i.Version > since)
                    .Select(i => new GoogleEventChange(i.Id, i.Deleted ? null : ToData(i)));
            }
            return Task.FromResult(new GoogleEventPage(changes.ToList(), $"v{_version}"));
        }
    }

    public Task<GoogleEventData> InsertEventAsync(string userId, string googleCalendarId, GoogleEventInput input, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var id = Put(googleCalendarId, input.Title, input.Start, input.End, input.Description, input.AllDay);
            var item = _items.First(i => i.CalendarId == googleCalendarId && i.Id == id);
            item.Input = input;
            return Task.FromResult(ToData(item));
        }
    }

    public Task<GoogleEventData> UpdateEventAsync(string userId, string googleCalendarId, string eventId, string? etag,
        GoogleEventInput input, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(i => i.CalendarId == googleCalendarId && i.Id == eventId && !i.Deleted)
                ?? throw new NotFoundException("Begivenheden findes ikke i Google.");
            if (etag is not null && etag != ETag(item)) throw new GoogleConflictException();
            item.Input = input;
            item.Version = ++_version;
            return Task.FromResult(ToData(item));
        }
    }

    public Task DeleteEventAsync(string userId, string googleCalendarId, string eventId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(i => i.CalendarId == googleCalendarId && i.Id == eventId && !i.Deleted);
            if (item is not null)
            {
                item.Deleted = true;
                item.Version = ++_version;
            }
            return Task.CompletedTask;
        }
    }

    private static string ETag(Item i) => $"\"{i.Version}\"";

    private GoogleEventData ToData(Item i) => new(i.Id, ETag(i), i.Input.Title, i.Input.Description, i.Input.Location,
        i.Input.Start, i.Input.End, i.Input.AllDay, clock.UtcNow);

    // ---- IGoogleAuthService (always connected) ----

    public bool IsConfigured => true;
    public Task<bool> IsConnectedAsync(string userId, CancellationToken ct = default) => Task.FromResult(true);
    public string BuildAuthorizationUrl(string state, string redirectUri) =>
        $"{redirectUri}?code=fake&state={Uri.EscapeDataString(state)}";
    public Task ConnectAsync(string userId, string code, string redirectUri, CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
}

