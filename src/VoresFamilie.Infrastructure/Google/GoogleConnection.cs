namespace VoresFamilie.Infrastructure;

/// <summary>A member's Google refresh token, encrypted with ASP.NET Data Protection.</summary>
public sealed class GoogleConnection
{
    public string UserId { get; set; } = "";
    public string EncryptedRefreshToken { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; set; }
}
