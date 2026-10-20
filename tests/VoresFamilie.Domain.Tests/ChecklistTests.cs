using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class ChecklistTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static (Family Family, FamilyMember Admin, FamilyMember Child, FamilyMember Sibling) NewFamily()
    {
        var f = Family.Create("F", "u-admin", "Admin");
        var child = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-child", "Emma", Now);
        var sibling = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-sib", "Noah", Now);
        return (f, f.Members[0], child, sibling);
    }

    [Fact]
    public void Days_include_the_right_weekdays()
    {
        Assert.True(DaysOfWeek.Weekdays.Includes(DayOfWeek.Friday));
        Assert.False(DaysOfWeek.Weekdays.Includes(DayOfWeek.Saturday));
        Assert.True(DaysOfWeek.All.Includes(DayOfWeek.Sunday));
        Assert.True((DaysOfWeek.Monday | DaysOfWeek.Wednesday).Includes(DayOfWeek.Wednesday));
        Assert.False(DaysOfWeek.None.Includes(DayOfWeek.Monday));
    }

    [Fact]
    public void Routine_needs_a_child_and_a_part()
    {
        var fam = Guid.NewGuid();
        Assert.Throws<DomainException>(() => ChecklistItem.Create(fam, ChecklistKind.Routine, RoutinePart.Morning, null, "Børst tænder", DaysOfWeek.All, 0));
        Assert.Throws<DomainException>(() => ChecklistItem.Create(fam, ChecklistKind.Routine, null, Guid.NewGuid(), "Børst tænder", DaysOfWeek.All, 0));
    }

    [Fact]
    public void Household_task_may_have_no_owner_and_never_has_a_part()
    {
        var item = ChecklistItem.Create(Guid.NewGuid(), ChecklistKind.HouseholdTask, RoutinePart.Evening, null, "Rydde køkken", DaysOfWeek.All, 0);
        Assert.Null(item.MemberId);
        Assert.Null(item.Part);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Title_is_required(string title) =>
        Assert.Throws<DomainException>(() => ChecklistItem.Create(Guid.NewGuid(), ChecklistKind.HouseholdTask, null, null, title, DaysOfWeek.All, 0));

    [Fact]
    public void At_least_one_weekday_is_required() =>
        Assert.Throws<DomainException>(() => ChecklistItem.Create(Guid.NewGuid(), ChecklistKind.HouseholdTask, null, null, "x", DaysOfWeek.None, 0));

    [Fact]
    public void Item_is_due_only_on_its_days()
    {
        var item = ChecklistItem.Create(Guid.NewGuid(), ChecklistKind.HouseholdTask, null, null, "Madpakker", DaysOfWeek.Weekdays, 0);
        Assert.True(item.IsDueOn(new DateOnly(2026, 10, 19)));  // Monday
        Assert.False(item.IsDueOn(new DateOnly(2026, 10, 24))); // Saturday
    }

    [Fact]
    public void Child_ticks_own_routine_but_not_a_siblings()
    {
        var (f, _, child, sibling) = NewFamily();
        var routine = ChecklistItem.Create(f.Id, ChecklistKind.Routine, RoutinePart.Morning, child.Id, "Børst tænder", DaysOfWeek.All, 0);
        Assert.True(ChecklistRules.CanCheck(child, routine));
        Assert.False(ChecklistRules.CanCheck(sibling, routine));
        Assert.True(ChecklistRules.CanView(child, routine));
    }

    [Fact]
    public void Child_blocked_from_checking_own_items_cannot_tick()
    {
        var (f, _, child, _) = NewFamily();
        var routine = ChecklistItem.Create(f.Id, ChecklistKind.Routine, RoutinePart.Morning, child.Id, "x", DaysOfWeek.All, 0);
        f.SetCanCheckOwnItems(child.Id, false);
        Assert.False(ChecklistRules.CanCheck(child, routine));
    }

    [Fact]
    public void Admin_and_editors_can_tick_for_others_and_edit()
    {
        var (f, admin, child, sibling) = NewFamily();
        var routine = ChecklistItem.Create(f.Id, ChecklistKind.Routine, RoutinePart.Evening, child.Id, "x", DaysOfWeek.All, 0);
        Assert.True(ChecklistRules.CanCheck(admin, routine));
        Assert.True(ChecklistRules.CanEdit(admin));
        Assert.False(ChecklistRules.CanEdit(child));
        f.SetPermission(sibling.Id, Area.Routines, AccessLevel.Edit);
        Assert.True(ChecklistRules.CanEdit(sibling));
        Assert.True(ChecklistRules.CanCheck(sibling, routine));
    }

    [Fact]
    public void Task_for_everyone_can_be_ticked_by_any_child_with_access()
    {
        var (f, _, child, _) = NewFamily();
        var everyone = ChecklistItem.Create(f.Id, ChecklistKind.HouseholdTask, null, null, "Dække bord", DaysOfWeek.All, 0);
        Assert.True(ChecklistRules.CanCheck(child, everyone));
        f.SetPermission(child.Id, Area.Routines, AccessLevel.None);
        Assert.False(ChecklistRules.CanCheck(child, everyone));
    }

    [Fact]
    public void Assigned_task_belongs_to_that_member_only()
    {
        var (f, _, child, sibling) = NewFamily();
        var task = ChecklistItem.Create(f.Id, ChecklistKind.HouseholdTask, null, child.Id, "Smøre madpakker", DaysOfWeek.Weekdays, 0);
        Assert.True(ChecklistRules.CanCheck(child, task));
        Assert.False(ChecklistRules.CanCheck(sibling, task));
    }

    [Fact]
    public void Owner_can_see_own_routine_even_without_area_access()
    {
        var (f, _, child, _) = NewFamily();
        var routine = ChecklistItem.Create(f.Id, ChecklistKind.Routine, RoutinePart.Morning, child.Id, "x", DaysOfWeek.All, 0);
        f.SetPermission(child.Id, Area.Routines, AccessLevel.None);
        Assert.True(ChecklistRules.CanView(child, routine));
    }
}

public class TodoTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Todo_requires_a_title() =>
        Assert.Throws<DomainException>(() => TodoItem.Create(Guid.NewGuid(), " ", null, null, null, Guid.NewGuid(), Now));

    [Fact]
    public void Complete_and_reopen()
    {
        var who = Guid.NewGuid();
        var todo = TodoItem.Create(Guid.NewGuid(), "Købe gave", "  til mormor ", null, null, who, Now);
        Assert.Equal("til mormor", todo.Notes);
        todo.Complete(who, Now.AddHours(1));
        todo.Complete(Guid.NewGuid(), Now.AddHours(2)); // second tick changes nothing
        Assert.True(todo.IsDone);
        Assert.Equal(who, todo.CompletedByMemberId);
        Assert.Equal(Now.AddHours(1), todo.CompletedAt);
        todo.Reopen();
        Assert.False(todo.IsDone);
        Assert.Null(todo.CompletedByMemberId);
    }

    [Fact]
    public void Overdue_only_when_open_and_past_due()
    {
        var todo = TodoItem.Create(Guid.NewGuid(), "x", null, null, new DateOnly(2026, 10, 18), Guid.NewGuid(), Now);
        Assert.True(todo.IsOverdue(new DateOnly(2026, 10, 19)));
        Assert.False(todo.IsOverdue(new DateOnly(2026, 10, 18)));
        todo.Complete(Guid.NewGuid(), Now);
        Assert.False(todo.IsOverdue(new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void Assignee_can_tick_own_todo_without_edit_rights()
    {
        var f = Family.Create("F", "u-admin", "Admin");
        var child = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-c", "Emma", Now);
        var other = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-o", "Noah", Now);
        var todo = TodoItem.Create(f.Id, "Tømme opvasker", null, child.Id, null, f.Members[0].Id, Now);
        Assert.False(TodoRules.CanEdit(child));
        Assert.True(TodoRules.CanCheck(child, todo));
        Assert.False(TodoRules.CanCheck(other, todo));
        Assert.True(TodoRules.CanCheck(f.Members[0], todo));
    }
}
