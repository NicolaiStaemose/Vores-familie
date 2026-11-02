using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

internal sealed class PushRepository(AppDbContext db) : IPushRepository
{
    public async Task<IReadOnlyList<PushSubscription>> GetSubscriptionsAsync(string userId, CancellationToken ct = default) =>
        await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);

    public Task<PushSubscription?> FindSubscriptionAsync(string endpoint, CancellationToken ct = default) =>
        db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

    public async Task<IReadOnlyList<string>> GetSubscribedUserIdsAsync(CancellationToken ct = default) =>
        await db.PushSubscriptions.Select(s => s.UserId).Distinct().ToListAsync(ct);

    public async Task AddSubscriptionAsync(PushSubscription subscription, CancellationToken ct = default) =>
        await db.PushSubscriptions.AddAsync(subscription, ct);

    public Task RemoveSubscriptionAsync(PushSubscription subscription, CancellationToken ct = default)
    {
        db.PushSubscriptions.Remove(subscription);
        return Task.CompletedTask;
    }

    public Task<NotificationSettings?> GetSettingsAsync(string userId, CancellationToken ct = default) =>
        db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task AddSettingsAsync(NotificationSettings settings, CancellationToken ct = default) =>
        await db.NotificationSettings.AddAsync(settings, ct);

    public Task<bool> HasLogAsync(string userId, DateOnly date, PushSlot slot, CancellationToken ct = default) =>
        db.NotificationLogs.AnyAsync(l => l.UserId == userId && l.Date == date && l.Slot == slot, ct);

    public async Task AddLogAsync(NotificationLog log, CancellationToken ct = default) => await db.NotificationLogs.AddAsync(log, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

/// <summary>Web Push with VAPID. Needs Push:PublicKey and Push:PrivateKey; without them push is switched off.</summary>
public sealed class WebPushSender : IPushSender
{
    private readonly WebPush.VapidDetails? _vapid;
    private readonly WebPush.WebPushClient _client = new();
    private readonly ILogger<WebPushSender> _log;

    public WebPushSender(IConfiguration config, ILogger<WebPushSender> log)
    {
        _log = log;
        var publicKey = config["Push:PublicKey"];
        var privateKey = config["Push:PrivateKey"];
        if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey))
            _vapid = new WebPush.VapidDetails(config["Push:Subject"] ?? "mailto:admin@example.com", publicKey, privateKey);
    }

    public bool IsConfigured => _vapid is not null;
    public string? PublicKey => _vapid?.PublicKey;

    public async Task<PushSendResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default)
    {
        if (_vapid is null) return PushSendResult.Failed;
        var payload = JsonSerializer.Serialize(new { title = message.Title, body = message.Body, url = message.Url });
        try
        {
            await _client.SendNotificationAsync(new WebPush.PushSubscription(target.Endpoint, target.P256dh, target.Auth), payload, _vapid, ct);
            return PushSendResult.Sent;
        }
        catch (WebPush.WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
        {
            return PushSendResult.Expired; // the user removed the app or revoked permission
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Push notification could not be sent");
            return PushSendResult.Failed;
        }
    }

    /// <summary>Makes a new VAPID key pair (run once; the keys go in user-secrets or the host's secrets).</summary>
    public static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        var keys = WebPush.VapidHelper.GenerateVapidKeys();
        return (keys.PublicKey, keys.PrivateKey);
    }
}

/// <summary>Looks every minute whether the 07:00 or 19:00 reminder is due. Needs the API to be running at that time.</summary>
public sealed class PushHostedService(IServiceScopeFactory scopes, ILogger<PushHostedService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<PushDispatcher>().RunAsync(stoppingToken);
                if (sent > 0) log.LogInformation("Push: sent {Count} notifications", sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Push reminders failed");
            }
        } while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); } catch (OperationCanceledException) { return false; }
    }
}
