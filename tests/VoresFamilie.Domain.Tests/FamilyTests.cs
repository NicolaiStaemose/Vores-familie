using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class FamilyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static Family NewFamily() => Family.Create("Familien", "u-admin", "Nicolai");

    [Fact]
    public void Create_makes_creator_admin_with_edit_everywhere()
    {
        var f = NewFamily();
        var m = Assert.Single(f.Members);
        Assert.Equal(Role.Admin, m.Role);
        Assert.All(Enum.GetValues<Area>(), a => Assert.Equal(AccessLevel.Edit, m.Permissions.For(a)));
    }

    [Fact]
    public void Create_requires_name() =>
        Assert.Throws<DomainException>(() => Family.Create(" ", "u", "n"));

    [Fact]
    public void Child_defaults_to_view_only_but_can_check_own_items()
    {
        var f = NewFamily();
        var token = f.Invite(Role.Barn, null, Now);
        var child = f.AcceptInvitation(token, "u-emma", "Emma", Now);
        Assert.All(Enum.GetValues<Area>(), a => Assert.Equal(AccessLevel.View, child.Permissions.For(a)));
        Assert.True(child.Permissions.CanCheckOwnItems);
    }

    [Fact]
    public void Invitation_can_only_be_used_once()
    {
        var f = NewFamily();
        var token = f.Invite(Role.Voksen, null, Now);
        f.AcceptInvitation(token, "u1", "A", Now);
        Assert.Throws<DomainException>(() => f.AcceptInvitation(token, "u2", "B", Now));
    }

    [Fact]
    public void Expired_invitation_is_rejected()
    {
        var f = NewFamily();
        var token = f.Invite(Role.Barn, null, Now);
        Assert.Throws<DomainException>(() =>
            f.AcceptInvitation(token, "u1", "A", Now + Family.InvitationLifetime + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Unknown_token_is_rejected() =>
        Assert.Throws<DomainException>(() => NewFamily().AcceptInvitation("nope", "u1", "A", Now));

    [Fact]
    public void Same_user_cannot_join_twice()
    {
        var f = NewFamily();
        var t = f.Invite(Role.Voksen, null, Now);
        Assert.Throws<DomainException>(() => f.AcceptInvitation(t, "u-admin", "Nicolai", Now));
    }

    [Fact]
    public void Last_admin_cannot_be_demoted_or_removed()
    {
        var f = NewFamily();
        var admin = f.Members[0];
        Assert.Throws<DomainException>(() => f.ChangeRole(admin.Id, Role.Voksen));
        Assert.Throws<DomainException>(() => f.RemoveMember(admin.Id));
    }

    [Fact]
    public void Admin_can_be_demoted_when_another_admin_exists()
    {
        var f = NewFamily();
        var t = f.Invite(Role.Admin, null, Now);
        var second = f.AcceptInvitation(t, "u2", "B", Now);
        f.ChangeRole(f.Members[0].Id, Role.Voksen);
        Assert.Equal(Role.Admin, second.Role);
    }

    [Fact]
    public void SetPermission_changes_one_area_only()
    {
        var f = NewFamily();
        var child = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-c", "C", Now);
        f.SetPermission(child.Id, Area.Shopping, AccessLevel.Edit);
        Assert.Equal(AccessLevel.Edit, child.Permissions.For(Area.Shopping));
        Assert.Equal(AccessLevel.View, child.Permissions.For(Area.Todo));
        Assert.True(child.Permissions.Allows(Area.Shopping, AccessLevel.Edit));
        Assert.False(child.Permissions.Allows(Area.Todo, AccessLevel.Edit));
    }

    [Fact]
    public void Admin_permissions_cannot_be_changed()
    {
        var f = NewFamily();
        Assert.Throws<DomainException>(() => f.SetPermission(f.Members[0].Id, Area.Todo, AccessLevel.None));
    }

    [Fact]
    public void ChangeRole_resets_permissions_to_role_defaults()
    {
        var f = NewFamily();
        var m = f.AcceptInvitation(f.Invite(Role.Voksen, null, Now), "u2", "B", Now);
        f.ChangeRole(m.Id, Role.Barn);
        Assert.Equal(AccessLevel.View, m.Permissions.For(Area.Calendars));
    }

    [Fact]
    public void Revoked_invitation_cannot_be_used()
    {
        var f = NewFamily();
        var t = f.Invite(Role.Barn, null, Now);
        f.RevokeInvitation(f.Invitations[0].Id);
        Assert.Throws<DomainException>(() => f.AcceptInvitation(t, "u2", "B", Now));
    }
}
