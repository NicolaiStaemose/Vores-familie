using System.Security.Cryptography;
using System.Text;

namespace VoresFamilie.Domain;

public sealed class Invitation
{
    private Invitation() { }

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; } = "";
    public Role Role { get; private set; }
    public string? Email { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    internal static (Invitation Invitation, string Token) Create(Role role, string? email, DateTimeOffset now, TimeSpan lifetime)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            TokenHash = Hash(token),
            Role = role,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            ExpiresAt = now + lifetime,
        };
        return (invitation, token);
    }

    internal void MarkUsed(DateTimeOffset now) => UsedAt = now;

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
