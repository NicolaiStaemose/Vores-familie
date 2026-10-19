namespace VoresFamilie.Domain;

/// <summary>A Google calendar that is shown in the app (family, personal or a child's homework calendar).</summary>
public sealed class FamilyCalendar
{
    private FamilyCalendar() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public CalendarKind Kind { get; private set; }
    /// <summary>Personal and Homework calendars belong to a member.</summary>
    public Guid? OwnerMemberId { get; private set; }
    public string GoogleCalendarId { get; private set; } = "";
    /// <summary>Identity user whose Google account the calendar is read from.</summary>
    public string ConnectedByUserId { get; private set; } = "";
    public string Color { get; private set; } = "#1b6ef3";
    public string? SyncToken { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    public static FamilyCalendar Create(Guid familyId, string name, CalendarKind kind, Guid? ownerMemberId,
        string googleCalendarId, string connectedByUserId, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Kalenderen skal have et navn.");
        if (string.IsNullOrWhiteSpace(googleCalendarId)) throw new DomainException("Google-kalender mangler.");
        if (kind != CalendarKind.Family && ownerMemberId is null)
            throw new DomainException("Personlige kalendere og lektiekalendere skal have en ejer.");
        return new FamilyCalendar
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            Name = name.Trim(),
            Kind = kind,
            OwnerMemberId = kind == CalendarKind.Family ? null : ownerMemberId,
            GoogleCalendarId = googleCalendarId,
            ConnectedByUserId = connectedByUserId,
            Color = string.IsNullOrWhiteSpace(color) ? "#1b6ef3" : color,
        };
    }

    public void RecordSync(string? syncToken, DateTimeOffset now)
    {
        SyncToken = syncToken;
        LastSyncedAt = now;
    }

    /// <summary>Forget the incremental sync state, forcing a full re-sync next time (Google answered 410 Gone).</summary>
    public void ResetSync() => SyncToken = null;

    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => LastSyncedAt is null || now - LastSyncedAt > maxAge;
}
