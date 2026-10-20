namespace VoresFamilie.Domain;

/// <summary>Who may see, change and tick off routines and household tasks (area <see cref="Area.Routines"/>).</summary>
public static class ChecklistRules
{
    private static bool IsOwner(FamilyMember actor, ChecklistItem item) => item.MemberId == actor.Id;

    public static bool CanSeeArea(FamilyMember actor) => actor.Permissions.Allows(Area.Routines, AccessLevel.View);

    public static bool CanView(FamilyMember actor, ChecklistItem item) => CanSeeArea(actor) || IsOwner(actor, item);

    public static bool CanEdit(FamilyMember actor) => actor.Permissions.Allows(Area.Routines, AccessLevel.Edit);

    /// <summary>
    /// The responsible person (or anyone, for a task without an owner) may tick it when allowed to check their own items;
    /// anyone with Edit may tick for others.
    /// </summary>
    public static bool CanCheck(FamilyMember actor, ChecklistItem item) =>
        CanEdit(actor)
        || (actor.Permissions.CanCheckOwnItems
            && (IsOwner(actor, item) || (item.Kind == ChecklistKind.HouseholdTask && item.MemberId is null && CanSeeArea(actor))));
}

/// <summary>Who may use the shared ToDo list (area <see cref="Area.Todo"/>).</summary>
public static class TodoRules
{
    public static bool CanView(FamilyMember actor, TodoItem item) =>
        actor.Permissions.Allows(Area.Todo, AccessLevel.View) || item.AssigneeMemberId == actor.Id;

    public static bool CanEdit(FamilyMember actor) => actor.Permissions.Allows(Area.Todo, AccessLevel.Edit);

    public static bool CanCheck(FamilyMember actor, TodoItem item) =>
        CanEdit(actor) || (actor.Permissions.CanCheckOwnItems && item.AssigneeMemberId == actor.Id);
}
