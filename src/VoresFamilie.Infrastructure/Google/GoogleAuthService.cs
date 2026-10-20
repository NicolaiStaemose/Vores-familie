using System.Collections.Concurrent;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using GoogleCalendarService = Google.Apis.Calendar.v3.CalendarService;
using Google.Apis.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

/// <summary>OAuth connection to a member's Google account and the credentials used for Calendar calls.</summary>
public sealed class GoogleAuthService(IOptions<GoogleCalendarOptions> options, IServiceScopeFactory scopes,
    IDataProtectionProvider dataProtection, IClock clock) : IGoogleAuthService
{
    private const string Purpose = "VoresFamilie.GoogleRefreshToken.v1";
    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);
    private readonly ConcurrentDictionary<string, UserCredential> _credentials = new();
    private readonly GoogleCalendarOptions _o = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ClientId) && !string.IsNullOrWhiteSpace(_o.ClientSecret);

    public async Task<bool> IsConnectedAsync(string userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GoogleConnections.AnyAsync(c => c.UserId == userId, ct);
    }

    public string BuildAuthorizationUrl(string state, string redirectUri) =>
        new GoogleAuthorizationCodeRequestUrl(new Uri(GoogleAuthConsts.AuthorizationUrl))
        {
            ClientId = _o.ClientId,
            RedirectUri = redirectUri,
            Scope = GoogleCalendarService.Scope.Calendar,
            AccessType = "offline",   // gives a refresh token
            Prompt = "consent",       // always returns the refresh token, also when reconnecting
            State = state,
            ResponseType = "code",
        }.Build().AbsoluteUri;

    public async Task ConnectAsync(string userId, string code, string redirectUri, CancellationToken ct = default)
    {
        var token = await CreateFlow().ExchangeCodeForTokenAsync(userId, code, redirectUri, ct);
        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new DomainException("Google gav ingen langtidsadgang. Fjern appens adgang på myaccount.google.com/permissions og prøv igen.");

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.GoogleConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (row is null) db.GoogleConnections.Add(row = new GoogleConnection { UserId = userId });
        row.EncryptedRefreshToken = _protector.Protect(token.RefreshToken);
        row.ConnectedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        _credentials.TryRemove(userId, out _);
    }

    public async Task DisconnectAsync(string userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.GoogleConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (row is null) return;
        try { await CreateFlow().RevokeTokenAsync(userId, _protector.Unprotect(row.EncryptedRefreshToken), ct); }
        catch { /* the token may already be revoked; we still forget it */ }
        db.GoogleConnections.Remove(row);
        await db.SaveChangesAsync(ct);
        _credentials.TryRemove(userId, out _);
    }

    /// <summary>A calendar client for the user. The access token is refreshed automatically.</summary>
    internal async Task<GoogleCalendarService> CreateServiceAsync(string userId, CancellationToken ct)
    {
        if (!IsConfigured) throw new DomainException("Google Kalender er ikke sat op endnu.");
        if (!_credentials.TryGetValue(userId, out var credential))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.GoogleConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct)
                ?? throw new DomainException("Google-kontoen er ikke forbundet. Forbind den først.");
            var refresh = _protector.Unprotect(row.EncryptedRefreshToken);
            credential = _credentials.GetOrAdd(userId, _ => new UserCredential(CreateFlow(), userId, new TokenResponse { RefreshToken = refresh }));
        }
        return new GoogleCalendarService(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = "Vores familie" });
    }

    /// <summary>Google no longer accepts the refresh token (revoked or expired): forget it so the user is asked to reconnect.</summary>
    internal async Task ForgetAsync(string userId)
    {
        _credentials.TryRemove(userId, out _);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GoogleConnections.RemoveRange(db.GoogleConnections.Where(c => c.UserId == userId));
        await db.SaveChangesAsync();
    }

    private GoogleAuthorizationCodeFlow CreateFlow() => new(new GoogleAuthorizationCodeFlow.Initializer
    {
        ClientSecrets = new ClientSecrets { ClientId = _o.ClientId, ClientSecret = _o.ClientSecret },
        Scopes = [GoogleCalendarService.Scope.Calendar],
    });
}

