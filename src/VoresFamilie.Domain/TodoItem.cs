namespace VoresFamilie.Domain;

public sealed class TodoItem
{
    private TodoItem() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }
    public Guid? AssigneeMemberId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? CompletedByMemberId { get; private set; }

    public bool IsDone => CompletedAt is not null;

    public static TodoItem Create(Guid familyId, string title, string? notes, Guid? assignee, DateOnly? due,
        Guid createdBy, DateTimeOffset now)
    {
        var item = new TodoItem { Id = Guid.NewGuid(), FamilyId = familyId, CreatedByMemberId = createdBy, CreatedAt = now };
        item.Apply(title, notes, assignee, due);
        return item;
    }

    public void Apply(string title, string? notes, Guid? assignee, DateOnly? due)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Punktet skal have en tekst.");
        Title = title.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        AssigneeMemberId = assignee;
        DueDate = due;
    }

    public void Complete(Guid memberId, DateTimeOffset now)
    {
        if (IsDone) return;
        CompletedAt = now;
        CompletedByMemberId = memberId;
    }

    public void Reopen()
    {
        CompletedAt = null;
        CompletedByMemberId = null;
    }

    public bool IsOverdue(DateOnly today) => !IsDone && DueDate is { } d && d < today;
}
