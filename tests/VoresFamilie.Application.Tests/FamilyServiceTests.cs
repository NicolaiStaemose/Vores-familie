using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Application.Tests;

public class FamilyServiceTests
{
    private sealed class FakeRepo : IFamilyRepository
    {
        public List<Family> Families { get; } = [];
        public Task<Family?> GetByUserAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Families.FirstOrDefault(f => f.FindMemberByUser(userId) is not null));
        public Task<Family?> GetByInvitationTokenAsync(string token, CancellationToken ct = default)
        {
            var hash = Invitation.Hash(token);
            return Task.FromResult(Families.FirstOrDefault(f => f.Invitations.Any(i => i.TokenHash == hash)));
        }
        public Task AddAsync(Family family, CancellationToken ct = default) { Families.Add(family); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeRepo _repo = new();
    private readonly FixedClock _clock = new();
    private FamilyService Service => new(_repo, _clock);

    [Fact]
    public async Task User_cannot_create_two_families()
    {
        await Service.CreateAsync("u1", new CreateFamilyRequest("A", "X"));
        await Assert.ThrowsAsync<DomainException>(() => Service.CreateAsync("u1", new CreateFamilyRequest("B", "X")));
    }

    [Fact]
    public async Task Non_admin_cannot_invite()
    {
        await Service.CreateAsync("admin", new CreateFamilyRequest("A", "Admin"));
        var inv = await Service.InviteAsync("admin", new CreateInvitationRequest(Role.Barn, null));
        await Service.AcceptInvitationAsync("kid", inv.Token, new AcceptInvitationRequest("Kid"));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service.InviteAsync("kid", new CreateInvitationRequest(Role.Barn, null)));
    }

    [Fact]
    public async Task Outsider_gets_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service.ListMembersAsync("nobody"));
    }

    [Fact]
    public async Task Invitation_expires_with_clock()
    {
        await Service.CreateAsync("admin", new CreateFamilyRequest("A", "Admin"));
        var inv = await Service.InviteAsync("admin", new CreateInvitationRequest(Role.Voksen, null));
        _clock.UtcNow += TimeSpan.FromDays(8);
        await Assert.ThrowsAsync<DomainException>(() => Service.AcceptInvitationAsync("u2", inv.Token, new AcceptInvitationRequest("B")));
    }

    [Fact]
    public async Task Unknown_token_is_rejected() =>
        await Assert.ThrowsAsync<DomainException>(() => Service.AcceptInvitationAsync("u2", "bogus", new AcceptInvitationRequest("B")));
}
