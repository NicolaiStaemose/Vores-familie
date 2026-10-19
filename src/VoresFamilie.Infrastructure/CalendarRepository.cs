using Microsoft.EntityFrameworkCore;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

internal sealed class CalendarRepository(AppDbContext db) : ICalendarRepository
{
    public async Task<IReadOnlyList<FamilyCalendar>> GetCalendarsAsync(Guid familyId, CancellationToken ct = default) =>
        await db.Calendars.Where(c => c.FamilyId == familyId).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<FamilyCalendar>> GetAllCalendarsAsync(CancellationToken ct = default) =>
        await db.Calendars.ToListAsync(ct);

    public Task<FamilyCalendar?> GetCalendarAsync(Guid calendarId, CancellationToken ct = default) =>
        db.Calendars.FirstOrDefaultAsync(c => c.Id == calendarId, ct);

    public async Task AddCalendarAsync(FamilyCalendar calendar, CancellationToken ct = default) =>
        await db.Calendars.AddAsync(calendar, ct);

    public async Task RemoveCalendarAsync(FamilyCalendar calendar, CancellationToken ct = default)
    {
        db.CalendarEvents.RemoveRange(await db.CalendarEvents.Where(e => e.CalendarId == calendar.Id).ToListAsync(ct));
        db.HomeworkCompletions.RemoveRange(await db.HomeworkCompletions.Where(h => h.CalendarId == calendar.Id).ToListAsync(ct));
        db.Calendars.Remove(calendar);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(IReadOnlyCollection<Guid> calendarIds,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Events that overlap [from, to): they end after the range starts and start before it ends.
        var ids = calendarIds.ToList();
        return await db.CalendarEvents
            .Where(e => ids.Contains(e.CalendarId) && e.End > from && e.Start < to)
            .OrderBy(e => e.Start)
            .ToListAsync(ct);
    }

    public Task<CalendarEvent?> FindEventAsync(Guid calendarId, string googleEventId, CancellationToken ct = default) =>
        db.CalendarEvents.FirstOrDefaultAsync(e => e.CalendarId == calendarId && e.GoogleEventId == googleEventId, ct);

    public async Task AddEventAsync(CalendarEvent calendarEvent, CancellationToken ct = default) =>
        await db.CalendarEvents.AddAsync(calendarEvent, ct);

    public Task RemoveEventAsync(CalendarEvent calendarEvent, CancellationToken ct = default)
    {
        db.CalendarEvents.Remove(calendarEvent);
        return Task.CompletedTask;
    }

    public async Task ClearEventsAsync(Guid calendarId, CancellationToken ct = default) =>
        db.CalendarEvents.RemoveRange(await db.CalendarEvents.Where(e => e.CalendarId == calendarId).ToListAsync(ct));

    public async Task<IReadOnlyList<HomeworkCompletion>> GetCompletionsAsync(IReadOnlyCollection<Guid> calendarIds, CancellationToken ct = default)
    {
        var ids = calendarIds.ToList();
        return await db.HomeworkCompletions.Where(h => ids.Contains(h.CalendarId)).ToListAsync(ct);
    }

    public Task<HomeworkCompletion?> FindCompletionAsync(Guid calendarId, string googleEventId, CancellationToken ct = default) =>
        db.HomeworkCompletions.FirstOrDefaultAsync(h => h.CalendarId == calendarId && h.GoogleEventId == googleEventId, ct);

    public async Task AddCompletionAsync(HomeworkCompletion completion, CancellationToken ct = default) =>
        await db.HomeworkCompletions.AddAsync(completion, ct);

    public Task RemoveCompletionAsync(HomeworkCompletion completion, CancellationToken ct = default)
    {
        db.HomeworkCompletions.Remove(completion);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
