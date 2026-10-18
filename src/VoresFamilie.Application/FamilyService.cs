using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class FamilyService(IFamilyRepository repository, IClock clock)
{
    public async Task<FamilyDto> CreateAsync(string userId, CreateFamilyRequest request, CancellationToken ct = default)
    {
        if (await repository.GetByUserAsync(userId, ct) is not null)
            throw new DomainException("Du er allerede medlem af en familie.");
        var family = Family.Create(request.Name, userId, request.DisplayName);
        await repository.AddAsync(family, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(family, family.Members[0]);
    }

    public async Task<FamilyDto?> GetCurrentAsync(string userId, CancellationToken ct = default)
    {
        var family = await repository.GetByUserAsync(userId, ct);
        return family is null ? null : ToDto(family, family.FindMemberByUser(userId)!);
    }

    public async Task<FamilyDto> AcceptInvitationAsync(string userId, string token, AcceptInvitationRequest request, CancellationToken ct = default)
    {
        if (await repository.GetByUserAsync(userId, ct) is not null)
            throw new DomainException("Du er allerede medlem af en familie.");
        var family = await repository.GetByInvitationTokenAsync(token, ct)
            ?? throw new DomainException("Invitationen er ugyldig eller udløbet.");
        var member = family.AcceptInvitation(token, userId, request.DisplayName, clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return ToDto(family, member);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(string userId, CancellationToken ct = default)
    {
        var (family, _) = await LoadAsync(userId, adminOnly: false, ct);
        return family.Members.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<InvitationDto>> ListInvitationsAsync(string userId, CancellationToken ct = default)
    {
        var (family, _) = await LoadAsync(userId, adminOnly: true, ct);
        return family.Invitations
            .Select(i => new InvitationDto(i.Id, i.Role, i.Email, i.ExpiresAt, i.UsedAt is not null))
            .ToList();
    }

    public async Task<CreatedInvitationDto> InviteAsync(string userId, CreateInvitationRequest request, CancellationToken ct = default)
    {
        var (family, _) = await LoadAsync(userId, adminOnly: true, ct);
        var token = family.Invite(request.Role, request.Email, clock.UtcNow);
        await repository.SaveChangesAsync(ct);
        return new CreatedInvitationDto(family.Invitations[^1].Id, token);
    }

    public Task RevokeInvitationAsync(string userId, Guid invitationId, CancellationToken ct = default) =>
        MutateAsync(userId, (f, _) => f.RevokeInvitation(invitationId), ct);

    public Task ChangeRoleAsync(string userId, Guid memberId, ChangeRoleRequest request, CancellationToken ct = default) =>
        MutateAsync(userId, (f, _) => f.ChangeRole(memberId, request.Role), ct);

    public Task SetPermissionAsync(string userId, Guid memberId, SetPermissionRequest request, CancellationToken ct = default) =>
        MutateAsync(userId, (f, _) => f.SetPermission(memberId, request.Area, request.Level), ct);

    public Task SetCanCheckOwnItemsAsync(string userId, Guid memberId, SetCanCheckOwnItemsRequest request, CancellationToken ct = default) =>
        MutateAsync(userId, (f, _) => f.SetCanCheckOwnItems(memberId, request.Value), ct);

    public Task RemoveMemberAsync(string userId, Guid memberId, CancellationToken ct = default) =>
        MutateAsync(userId, (f, _) => f.RemoveMember(memberId), ct);

    private async Task MutateAsync(string userId, Action<Family, FamilyMember> action, CancellationToken ct)
    {
        var (family, me) = await LoadAsync(userId, adminOnly: true, ct);
        action(family, me);
        await repository.SaveChangesAsync(ct);
    }

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, bool adminOnly, CancellationToken ct)
    {
        var family = await repository.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        var me = family.FindMemberByUser(userId)!;
        if (adminOnly && me.Role != Role.Admin) throw new ForbiddenException("Kun admin kan gøre dette.");
        return (family, me);
    }

    private static MemberDto ToDto(FamilyMember m) =>
        new(m.Id, m.DisplayName, m.Role, m.Permissions.Levels.ToDictionary(x => x.Key, x => x.Value), m.Permissions.CanCheckOwnItems);

    private static FamilyDto ToDto(Family f, FamilyMember me) => new(f.Id, f.Name, ToDto(me));
}
