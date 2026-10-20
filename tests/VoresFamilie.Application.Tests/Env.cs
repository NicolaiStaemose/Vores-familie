using VoresFamilie.Application;
using VoresFamilie.Domain;
using VoresFamilie.Infrastructure;

namespace VoresFamilie.Application.Tests;

/// <summary>In-memory repositories + fake Google, wired like production.</summary>
internal sealed class Env
{
    public sealed class MemFamilies : IFamilyRepository
    {
        public List<Family> All { get; } = [];
        public Task<Family?> GetByUserAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(All.FirstOrDefault(f => f.FindMemberByUser(userId) is not null));
        public Task<Family?> GetByInvitationTokenAsync(string token, CancellationToken ct = default) => Task.FromResult<Family?>(null);
        public Task AddAsync(Family family, CancellationToken ct = default) { All.Add(family); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    public sealed class MemCalendars : ICalendarRepository
    {
        public List<FamilyCalendar> Calendars { get; } = [];
        public List<CalendarEvent> Events { get; } = [];
        public List<HomeworkCompletion> Completions { get; } = [];
        public Task<IReadOnlyList<FamilyCalendar>> GetCalendarsAsync(Guid familyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FamilyCalendar>>(Calendars.Where(c => c.FamilyId == familyId).ToList());
        public Task<IReadOnlyList<FamilyCalendar>> GetAllCalendarsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FamilyCalendar>>(Calendars.ToList());
        public Task<FamilyCalendar?> GetCalendarAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Calendars.FirstOrDefault(c => c.Id == id));
        public Task AddCalendarAsync(FamilyCalendar c, CancellationToken ct = default) { Calendars.Add(c); return Task.CompletedTask; }
        public Task RemoveCalendarAsync(FamilyCalendar c, CancellationToken ct = default)
        {
            Calendars.Remove(c);
            Events.RemoveAll(e => e.CalendarId == c.Id);
            Completions.RemoveAll(h => h.CalendarId == c.Id);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>(Events.Where(e => ids.Contains(e.CalendarId) && e.End > from && e.Start < to).ToList());
        public Task<CalendarEvent?> FindEventAsync(Guid calendarId, string id, CancellationToken ct = default) =>
            Task.FromResult(Events.FirstOrDefault(e => e.CalendarId == calendarId && e.GoogleEventId == id));
        public Task AddEventAsync(CalendarEvent e, CancellationToken ct = default) { Events.Add(e); return Task.CompletedTask; }
        public Task RemoveEventAsync(CalendarEvent e, CancellationToken ct = default) { Events.Remove(e); return Task.CompletedTask; }
        public Task ClearEventsAsync(Guid calendarId, CancellationToken ct = default) { Events.RemoveAll(e => e.CalendarId == calendarId); return Task.CompletedTask; }
        public Task<IReadOnlyList<HomeworkCompletion>> GetCompletionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HomeworkCompletion>>(Completions.Where(h => ids.Contains(h.CalendarId)).ToList());
        public Task<HomeworkCompletion?> FindCompletionAsync(Guid calendarId, string id, CancellationToken ct = default) =>
            Task.FromResult(Completions.FirstOrDefault(h => h.CalendarId == calendarId && h.GoogleEventId == id));
        public Task AddCompletionAsync(HomeworkCompletion h, CancellationToken ct = default) { Completions.Add(h); return Task.CompletedTask; }
        public Task RemoveCompletionAsync(HomeworkCompletion h, CancellationToken ct = default) { Completions.Remove(h); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    public sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    }

    public Clock Time { get; } = new();
    public MemFamilies Families { get; } = new();
    public MemCalendars Calendars { get; } = new();
    public FakeGoogleCalendarGateway Google { get; }
    public CalendarSyncService Sync { get; }
    public CalendarService CalendarService { get; }
    public HomeworkService Homework { get; }

    public Family Family { get; }
    public FamilyMember Admin { get; }
    public FamilyMember Child { get; }
    public FamilyMember Sibling { get; }

    public const string AdminUser = "u-admin", ChildUser = "u-child", SiblingUser = "u-sibling";

    public Env()
    {
        Google = new FakeGoogleCalendarGateway(Time);
        Sync = new CalendarSyncService(Calendars, Google, Time);
        CalendarService = new CalendarService(Families, Calendars, Google, Google, Sync);
        Homework = new HomeworkService(Families, Calendars, Sync, Time);

        Family = Family.Create("F", AdminUser, "Admin");
        Admin = Family.Members[0];
        Child = Family.AcceptInvitation(Family.Invite(Role.Barn, null, Time.UtcNow), ChildUser, "Emma", Time.UtcNow);
        Sibling = Family.AcceptInvitation(Family.Invite(Role.Barn, null, Time.UtcNow), SiblingUser, "Noah", Time.UtcNow);
        Families.All.Add(Family);
    }

    public async Task<CalendarDto> AddAsync(string googleId, string name, CalendarKind kind, Guid? owner = null)
    {
        Google.AddCalendar(googleId, name);
        return await CalendarService.AddCalendarAsync(AdminUser, new AddCalendarRequest(googleId, name, kind, owner, null));
    }

    public DateTimeOffset Day(int offset, int hour = 0) => new DateTimeOffset(Time.UtcNow.Date, TimeSpan.Zero).AddDays(offset).AddHours(hour);

    public Task<IReadOnlyList<EventDto>> EventsAsync(string user, Guid? calendarId = null) =>
        CalendarService.GetEventsAsync(user, Day(-1), Day(30), calendarId is null ? null : [calendarId.Value]);
}
