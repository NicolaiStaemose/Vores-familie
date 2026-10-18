namespace VoresFamilie.Domain;

/// <summary>Aggregate root.</summary>
public sealed class Family
{
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly List<FamilyMember> _members = [];
    private readonly List<Invitation> _invitations = [];

    private Family() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public IReadOnlyList<FamilyMember> Members => _members;
    public IReadOnlyList<Invitation> Invitations => _invitations;

    public static Family Create(string name, string adminUserId, string adminDisplayName)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Familien skal have et navn.");
        var family = new Family { Id = Guid.NewGuid(), Name = name.Trim() };
        family._members.Add(FamilyMember.Create(adminUserId, adminDisplayName, Role.Admin));
        return family;
    }

    public FamilyMember? FindMemberByUser(string userId) => _members.FirstOrDefault(m => m.UserId == userId);

    public string Invite(Role role, string? email, DateTimeOffset now)
    {
        var (invitation, token) = Invitation.Create(role, email, now, InvitationLifetime);
        _invitations.Add(invitation);
        return token;
    }

    public void RevokeInvitation(Guid invitationId)
    {
        var inv = _invitations.FirstOrDefault(i => i.Id == invitationId)
            ?? throw new DomainException("Invitationen findes ikke.");
        _invitations.Remove(inv);
    }

    public FamilyMember AcceptInvitation(string token, string userId, string displayName, DateTimeOffset now)
    {
        var hash = Invitation.Hash(token);
        var invitation = _invitations.FirstOrDefault(i => i.TokenHash == hash);
        if (invitation is null || !invitation.IsUsable(now))
            throw new DomainException("Invitationen er ugyldig eller udløbet.");
        if (FindMemberByUser(userId) is not null)
            throw new DomainException("Brugeren er allerede medlem af familien.");

        invitation.MarkUsed(now);
        var member = FamilyMember.Create(userId, displayName, invitation.Role);
        _members.Add(member);
        return member;
    }

    public void ChangeRole(Guid memberId, Role role)
    {
        var member = GetMember(memberId);
        if (member.Role == Role.Admin && role != Role.Admin) EnsureAnotherAdmin(member);
        member.ChangeRole(role);
    }

    public void SetPermission(Guid memberId, Area area, AccessLevel level)
    {
        var member = GetMember(memberId);
        if (member.Role == Role.Admin) throw new DomainException("En admins rettigheder kan ikke ændres.");
        member.SetPermissions(member.Permissions.With(area, level));
    }

    public void SetCanCheckOwnItems(Guid memberId, bool value)
    {
        var member = GetMember(memberId);
        member.SetPermissions(member.Permissions.WithCanCheckOwnItems(value));
    }

    public void RemoveMember(Guid memberId)
    {
        var member = GetMember(memberId);
        if (member.Role == Role.Admin) EnsureAnotherAdmin(member);
        _members.Remove(member);
    }

    private FamilyMember GetMember(Guid id) =>
        _members.FirstOrDefault(m => m.Id == id) ?? throw new DomainException("Medlemmet findes ikke.");

    private void EnsureAnotherAdmin(FamilyMember member)
    {
        if (!_members.Any(m => m.Role == Role.Admin && m.Id != member.Id))
            throw new DomainException("Familien skal have mindst én admin.");
    }
}
