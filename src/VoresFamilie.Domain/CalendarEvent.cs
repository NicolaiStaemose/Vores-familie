namespace VoresFamilie.Domain;

/// <summary>Local cache of a Google Calendar event. Google is the source of truth.</summary>
public sealed class CalendarEvent
{
    private CalendarEvent() { }

    public Guid Id { get; private set; }
    public Guid CalendarId { get; private set; }
    public string GoogleEventId { get; private set; } = "";
    public string? ETag { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Location { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public bool AllDay { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsHomework => HomeworkParser.IsHomework(Title);

    public static CalendarEvent Create(Guid calendarId, string googleEventId, string? etag, string title, string? description,
        string? location, DateTimeOffset start, DateTimeOffset end, bool allDay, DateTimeOffset? updatedAt)
    {
        var e = new CalendarEvent { Id = Guid.NewGuid(), CalendarId = calendarId, GoogleEventId = googleEventId };
        e.Apply(etag, title, description, location, start, end, allDay, updatedAt);
        return e;
    }

    public void Apply(string? etag, string title, string? description, string? location,
        DateTimeOffset start, DateTimeOffset end, bool allDay, DateTimeOffset? updatedAt)
    {
        if (end < start) throw new DomainException("Slut kan ikke ligge før start.");
        ETag = etag;
        Title = string.IsNullOrWhiteSpace(title) ? "(uden titel)" : title.Trim();
        Description = description;
        Location = location;
        Start = start;
        End = end;
        AllDay = allDay;
        UpdatedAt = updatedAt;
    }
}
