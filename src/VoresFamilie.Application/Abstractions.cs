using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public interface IFamilyRepository
{
    Task<Family?> GetByUserAsync(string userId, CancellationToken ct = default);
    Task<Family?> GetByInvitationTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(Family family, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public class NotFoundException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);
