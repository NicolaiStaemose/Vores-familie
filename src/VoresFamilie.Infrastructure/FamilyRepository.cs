using Microsoft.EntityFrameworkCore;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

internal sealed class FamilyRepository(AppDbContext db) : IFamilyRepository
{
    private IQueryable<Family> Query => db.Families.Include(f => f.Members).Include(f => f.Invitations);

    public Task<Family?> GetByUserAsync(string userId, CancellationToken ct = default) =>
        Query.FirstOrDefaultAsync(f => f.Members.Any(m => m.UserId == userId), ct);

    public Task<Family?> GetByInvitationTokenAsync(string token, CancellationToken ct = default)
    {
        var hash = Invitation.Hash(token);
        return Query.FirstOrDefaultAsync(f => f.Invitations.Any(i => i.TokenHash == hash), ct);
    }

    public async Task AddAsync(Family family, CancellationToken ct = default) => await db.Families.AddAsync(family, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
