namespace VoresFamilie.Domain;

/// <summary>A child has ticked off a homework event. Keyed by calendar and Google event id; Google is never changed.</summary>
public sealed class HomeworkCompletion
{
    private HomeworkCompletion() { }

    public Guid Id { get; private set; }
    public Guid CalendarId { get; private set; }
    public string GoogleEventId { get; private set; } = "";
    public Guid MemberId { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }

    public static HomeworkCompletion Create(Guid calendarId, string googleEventId, Guid memberId, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), CalendarId = calendarId, GoogleEventId = googleEventId, MemberId = memberId, CompletedAt = now };
}
