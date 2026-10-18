namespace VoresFamilie.Domain;

public sealed class FamilyMember
{
    private FamilyMember() { }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public Role Role { get; private set; }
    public MemberPermissions Permissions { get; private set; } = MemberPermissions.DefaultsFor(Role.Barn);

    internal static FamilyMember Create(string userId, string displayName, Role role) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        DisplayName = displayName,
        Role = role,
        Permissions = MemberPermissions.DefaultsFor(role),
    };

    internal void ChangeRole(Role role)
    {
        Role = role;
        Permissions = MemberPermissions.DefaultsFor(role);
    }

    internal void SetPermissions(MemberPermissions permissions) => Permissions = permissions;
}
