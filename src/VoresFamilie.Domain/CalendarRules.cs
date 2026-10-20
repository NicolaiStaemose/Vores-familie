namespace VoresFamilie.Domain;

/// <summary>Who may see and change what. Admin always has Edit everywhere (see <see cref="MemberPermissions"/>).</summary>
public static class CalendarRules
{
    private static Area AreaFor(FamilyCalendar c) => c.Kind == CalendarKind.Homework ? Area.Homework : Area.Calendars;

    public static bool CanView(FamilyMember actor, FamilyCalendar calendar) =>
        actor.Permissions.Allows(AreaFor(calendar), AccessLevel.View) || actor.Id == calendar.OwnerMemberId;

    public static bool CanEdit(FamilyMember actor, FamilyCalendar calendar) =>
        actor.Permissions.Allows(AreaFor(calendar), AccessLevel.Edit)
        || (calendar.Kind == CalendarKind.Personal && actor.Id == calendar.OwnerMemberId);

    /// <summary>Ticking off homework: the owner (if allowed to), or anyone with Edit on homework.</summary>
    public static bool CanCheckHomework(FamilyMember actor, FamilyCalendar calendar) =>
        calendar.Kind == CalendarKind.Homework
        && ((actor.Id == calendar.OwnerMemberId && actor.Permissions.CanCheckOwnItems)
            || actor.Permissions.Allows(Area.Homework, AccessLevel.Edit));
}
