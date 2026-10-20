namespace VoresFamilie.Domain;

public enum ChecklistKind { Routine, HouseholdTask }

public enum RoutinePart { Morning, Evening }

/// <summary>
/// One line on a daily checklist: either a step in a child's morning/evening routine, or a household task
/// ("make dinner") with an optional responsible person. It repeats on the chosen weekdays.
/// </summary>
public sealed class ChecklistItem
{
    private ChecklistItem() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public ChecklistKind Kind { get; private set; }
    /// <summary>Only for routines.</summary>
    public RoutinePart? Part { get; private set; }
    /// <summary>Routine: the child it belongs to. Household task: the responsible member, or null for "everyone".</summary>
    public Guid? MemberId { get; private set; }
    public string Title { get; private set; } = "";
    public DaysOfWeek Days { get; private set; } = DaysOfWeek.All;
    public int SortOrder { get; private set; }
    /// <summary>null = automatic (suggested from the title), empty = no icon, otherwise an emoji chosen by an admin.</summary>
    public string? Icon { get; private set; }

    public static ChecklistItem Create(Guid familyId, ChecklistKind kind, RoutinePart? part, Guid? memberId,
        string title, DaysOfWeek days, int sortOrder, string? icon = null)
    {
        var item = new ChecklistItem { Id = Guid.NewGuid(), FamilyId = familyId, Kind = kind, SortOrder = sortOrder };
        item.Apply(part, memberId, title, days, icon);
        return item;
    }

    public void Apply(RoutinePart? part, Guid? memberId, string title, DaysOfWeek days, string? icon)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Punktet skal have en tekst.");
        if (days == DaysOfWeek.None) throw new DomainException("Vælg mindst én ugedag.");
        if (Kind == ChecklistKind.Routine)
        {
            if (memberId is null) throw new DomainException("En rutine skal høre til et barn.");
            if (part is null) throw new DomainException("Vælg morgen eller aften.");
        }
        Part = Kind == ChecklistKind.Routine ? part : null;
        MemberId = memberId;
        Title = title.Trim();
        Days = days;
        Icon = ChecklistIcons.Normalize(icon);
    }

    public void MoveTo(int sortOrder) => SortOrder = sortOrder;

    public bool IsDueOn(DateOnly date) => Days.Includes(date.DayOfWeek);
}

