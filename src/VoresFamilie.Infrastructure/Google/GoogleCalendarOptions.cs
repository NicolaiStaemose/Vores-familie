namespace VoresFamilie.Infrastructure;

/// <summary>Configuration section `Google:Calendar`. Secrets come from user-secrets or Container Apps secrets, never the repo.</summary>
public sealed class GoogleCalendarOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
