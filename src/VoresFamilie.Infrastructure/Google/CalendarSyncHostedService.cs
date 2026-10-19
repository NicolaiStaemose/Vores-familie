using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoresFamilie.Application;

namespace VoresFamilie.Infrastructure;

/// <summary>Pulls calendar changes from Google every 15 minutes, so data is fresh even when nobody has the app open.</summary>
public sealed class CalendarSyncHostedService(IServiceScopeFactory scopes, ILogger<CalendarSyncHostedService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (!scope.ServiceProvider.GetRequiredService<IGoogleAuthService>().IsConfigured) continue;
                var result = await scope.ServiceProvider.GetRequiredService<CalendarSyncService>().SyncAllAsync(stoppingToken);
                if (result.Calendars > 0) log.LogInformation("Calendar sync: {Calendars} calendars, {Failed} failed", result.Calendars, result.Failed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Calendar background sync failed");
            }
        } while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); } catch (OperationCanceledException) { return false; }
    }
}
