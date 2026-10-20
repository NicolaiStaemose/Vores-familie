using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class HomeworkParserTests
{
    [Theory]
    [InlineData("Lektie", true)]
    [InlineData("lektie", true)]
    [InlineData("Lektie: 5A eller 5B", true)]
    [InlineData("LEKTIE: Læs side 4", true)]
    [InlineData("99 arter", false)]
    [InlineData("Program", false)]
    [InlineData("Omfotografering", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsHomework_matches_lektie_in_any_casing(string? title, bool expected) =>
        Assert.Equal(expected, HomeworkParser.IsHomework(title));

    [Fact]
    public void Parse_reads_subject_and_child_lines()
    {
        var info = HomeworkParser.Parse("Læs kapitel 3\n\nBarn: Emma Hansen\nFag: Dansk");
        Assert.Equal("Dansk", info.Subject);
        Assert.Equal("Emma Hansen", info.ChildName);
    }

    [Fact]
    public void Parse_handles_crlf_and_missing_lines()
    {
        var info = HomeworkParser.Parse("Opgave\r\n\r\nBarn: Noah\r\n");
        Assert.Null(info.Subject);
        Assert.Equal("Noah", info.ChildName);
        Assert.Equal(new HomeworkInfo(null, null), HomeworkParser.Parse(null));
    }
}

public class CalendarModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static (Family Family, FamilyMember Admin, FamilyMember Child, FamilyMember Adult) NewFamily()
    {
        var f = Family.Create("F", "u-admin", "Admin");
        var child = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-child", "Emma", Now);
        var adult = f.AcceptInvitation(f.Invite(Role.Voksen, null, Now), "u-adult", "Voksen", Now);
        return (f, f.Members[0], child, adult);
    }

    [Fact]
    public void Personal_and_homework_calendars_need_an_owner()
    {
        Assert.Throws<DomainException>(() => FamilyCalendar.Create(Guid.NewGuid(), "x", CalendarKind.Personal, null, "g", "u"));
        Assert.Throws<DomainException>(() => FamilyCalendar.Create(Guid.NewGuid(), "x", CalendarKind.Homework, null, "g", "u"));
        var family = FamilyCalendar.Create(Guid.NewGuid(), "Fælles", CalendarKind.Family, Guid.NewGuid(), "g", "u");
        Assert.Null(family.OwnerMemberId); // owner is ignored for the family calendar
    }

    [Fact]
    public void Event_rejects_end_before_start() =>
        Assert.Throws<DomainException>(() =>
            CalendarEvent.Create(Guid.NewGuid(), "e", null, "t", null, null, Now, Now.AddHours(-1), false, null));

    [Fact]
    public void Event_knows_when_it_is_homework()
    {
        var e = CalendarEvent.Create(Guid.NewGuid(), "e", null, "Lektie: Dansk", null, null, Now, Now.AddHours(1), false, null);
        Assert.True(e.IsHomework);
    }

    [Fact]
    public void Child_can_view_but_not_edit_family_calendar_by_default()
    {
        var (f, _, child, _) = NewFamily();
        var cal = FamilyCalendar.Create(f.Id, "Fælles", CalendarKind.Family, null, "g", "u");
        Assert.True(CalendarRules.CanView(child, cal));
        Assert.False(CalendarRules.CanEdit(child, cal));
    }

    [Fact]
    public void Owner_can_always_edit_own_personal_calendar()
    {
        var (f, _, child, adult) = NewFamily();
        var cal = FamilyCalendar.Create(f.Id, "Emma", CalendarKind.Personal, child.Id, "g", "u");
        Assert.True(CalendarRules.CanEdit(child, cal));
        Assert.True(CalendarRules.CanEdit(adult, cal)); // adults default to Edit
        f.SetPermission(adult.Id, Area.Calendars, AccessLevel.View);
        Assert.False(CalendarRules.CanEdit(adult, cal));
    }

    [Fact]
    public void No_access_hides_calendar_but_not_from_its_owner()
    {
        var (f, _, child, _) = NewFamily();
        f.SetPermission(child.Id, Area.Calendars, AccessLevel.None);
        var shared = FamilyCalendar.Create(f.Id, "Fælles", CalendarKind.Family, null, "g", "u");
        var own = FamilyCalendar.Create(f.Id, "Emma", CalendarKind.Personal, child.Id, "g2", "u");
        Assert.False(CalendarRules.CanView(child, shared));
        Assert.True(CalendarRules.CanView(child, own));
    }

    [Fact]
    public void Child_can_check_own_homework_only_when_allowed()
    {
        var (f, _, child, adult) = NewFamily();
        var cal = FamilyCalendar.Create(f.Id, "Emma lektier", CalendarKind.Homework, child.Id, "g", "u");
        Assert.True(CalendarRules.CanCheckHomework(child, cal));
        f.SetCanCheckOwnItems(child.Id, false);
        Assert.False(CalendarRules.CanCheckHomework(child, cal));
        Assert.True(CalendarRules.CanCheckHomework(adult, cal)); // Edit on homework
    }

    [Fact]
    public void Child_cannot_check_another_childs_homework()
    {
        var (f, _, child, _) = NewFamily();
        var sibling = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-sib", "Noah", Now);
        var cal = FamilyCalendar.Create(f.Id, "Noah lektier", CalendarKind.Homework, sibling.Id, "g", "u");
        Assert.False(CalendarRules.CanCheckHomework(child, cal));
    }

    [Fact]
    public void Schedule_calendar_needs_an_owner_and_is_edited_with_calendar_rights()
    {
        var (f, _, child, adult) = NewFamily();
        Assert.Throws<DomainException>(() => FamilyCalendar.Create(f.Id, "Skema", CalendarKind.Schedule, null, "g", "u"));
        var cal = FamilyCalendar.Create(f.Id, "Emma skema", CalendarKind.Schedule, child.Id, "g", "u");
        Assert.True(CalendarRules.CanView(child, cal));
        Assert.False(CalendarRules.CanEdit(child, cal)); // the timetable is not the child's to change
        Assert.True(CalendarRules.CanEdit(adult, cal));
        Assert.False(CalendarRules.CanCheckHomework(adult, cal)); // not a homework calendar
    }

    [Fact]
    public void Sync_state_can_be_recorded_and_reset()
    {
        var cal = FamilyCalendar.Create(Guid.NewGuid(), "K", CalendarKind.Family, null, "g", "u");
        Assert.True(cal.IsStale(Now, TimeSpan.FromMinutes(2)));
        cal.RecordSync("tok", Now);
        Assert.False(cal.IsStale(Now.AddMinutes(1), TimeSpan.FromMinutes(2)));
        Assert.True(cal.IsStale(Now.AddMinutes(3), TimeSpan.FromMinutes(2)));
        cal.ResetSync();
        Assert.Null(cal.SyncToken);
    }
}

