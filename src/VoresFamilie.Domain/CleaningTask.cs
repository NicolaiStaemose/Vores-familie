namespace VoresFamilie.Domain;

public enum CleaningStatus { NeverDone, Overdue, DueToday, Upcoming }

/// <summary>A recurring cleaning job ("Støvsug stuen" every 7 days). It remembers when it was last done.</summary>
public sealed class CleaningTask
{
    /// <summary>How far back a "done" can be registered (so "I did it yesterday" is possible).</summary>
    public const int MaxDaysBack = 14;

    private CleaningTask() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public string Room { get; private set; } = "";
    public int IntervalDays { get; private set; }
    public Guid? AssigneeMemberId { get; private set; }
    public DateOnly CreatedOn { get; private set; }
    public DateOnly? LastDoneOn { get; private set; }
    public Guid? LastDoneByMemberId { get; private set; }

    public static CleaningTask Create(Guid familyId, string name, string? room, int intervalDays, Guid? assignee, DateOnly today)
    {
        var task = new CleaningTask { Id = Guid.NewGuid(), FamilyId = familyId, CreatedOn = today };
        task.Apply(name, room, intervalDays, assignee);
        return task;
    }

    public void Apply(string name, string? room, int intervalDays, Guid? assignee)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Opgaven skal have et navn.");
        if (intervalDays < 1 || intervalDays > 365) throw new DomainException("Intervallet skal være mellem 1 og 365 dage.");
        Name = name.Trim();
        Room = string.IsNullOrWhiteSpace(room) ? "Andet" : room.Trim();
        IntervalDays = intervalDays;
        AssigneeMemberId = assignee;
    }

    /// <summary>When it is next due. A task that was never done is due from the day it was created.</summary>
    public DateOnly NextDue => LastDoneOn?.AddDays(IntervalDays) ?? CreatedOn;

    public CleaningStatus StatusOn(DateOnly today)
    {
        if (LastDoneOn is null) return CleaningStatus.NeverDone;
        var due = NextDue;
        return due < today ? CleaningStatus.Overdue : due == today ? CleaningStatus.DueToday : CleaningStatus.Upcoming;
    }

    /// <summary>Whole days past the due date (0 or negative when not yet due).</summary>
    public int DaysOverdue(DateOnly today) => today.DayNumber - NextDue.DayNumber;

    public bool NeedsAttention(DateOnly today) => StatusOn(today) != CleaningStatus.Upcoming;

    public void MarkDone(DateOnly date, Guid memberId, DateOnly today)
    {
        if (date > today) throw new DomainException("Opgaven kan ikke være gjort i fremtiden.");
        if (date < today.AddDays(-MaxDaysBack)) throw new DomainException($"Du kan højst registrere {MaxDaysBack} dage tilbage.");
        if (LastDoneOn is { } last && date < last) throw new DomainException("Der er allerede registreret en nyere dato.");
        LastDoneOn = date;
        LastDoneByMemberId = memberId;
    }
}
