namespace VoresFamilie.Domain;

/// <summary>Who may see and change the meal plan (area <see cref="Area.MealPlan"/>).</summary>
public static class MealRules
{
    public static bool CanView(FamilyMember actor) => actor.Permissions.Allows(Area.MealPlan, AccessLevel.View);
    public static bool CanEdit(FamilyMember actor) => actor.Permissions.Allows(Area.MealPlan, AccessLevel.Edit);
}

/// <summary>Who may see and change the shopping list (area <see cref="Area.Shopping"/>).</summary>
public static class ShoppingRules
{
    public static bool CanView(FamilyMember actor) => actor.Permissions.Allows(Area.Shopping, AccessLevel.View);
    public static bool CanEdit(FamilyMember actor) => actor.Permissions.Allows(Area.Shopping, AccessLevel.Edit);
}

/// <summary>Who may see, change and mark cleaning jobs as done (area <see cref="Area.Cleaning"/>).</summary>
public static class CleaningRules
{
    public static bool CanView(FamilyMember actor, CleaningTask task) =>
        actor.Permissions.Allows(Area.Cleaning, AccessLevel.View) || task.AssigneeMemberId == actor.Id;

    public static bool CanEdit(FamilyMember actor) => actor.Permissions.Allows(Area.Cleaning, AccessLevel.Edit);

    /// <summary>
    /// Whoever is responsible may mark it done when allowed to check their own items; for a job nobody owns,
    /// anyone who may see it can. Editors can always mark it for others.
    /// </summary>
    public static bool CanMarkDone(FamilyMember actor, CleaningTask task) =>
        CanEdit(actor)
        || (actor.Permissions.CanCheckOwnItems
            && (task.AssigneeMemberId == actor.Id || (task.AssigneeMemberId is null && CanView(actor, task))));
}
