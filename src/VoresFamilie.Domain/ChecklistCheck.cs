namespace VoresFamilie.Domain;

/// <summary>A checklist item ticked off on a specific date. A new day starts with nothing ticked.</summary>
public sealed class ChecklistCheck
{
    private ChecklistCheck() { }

    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public Guid CompletedByMemberId { get; private set; }

    public static ChecklistCheck Create(Guid itemId, DateOnly date, Guid completedBy, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), ItemId = itemId, Date = date, CompletedByMemberId = completedBy, CompletedAt = now };
}
