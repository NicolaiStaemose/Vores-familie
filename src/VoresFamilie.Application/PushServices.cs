using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed record PushMessage(string Title, string Body, string Url);

public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

public enum PushSendResult { Sent, Expired, Failed }

/// <summary>Sends one notification to one device (Web Push).</summary>
public interface IPushSender
{
    /// <summary>False when no VAPID keys are configured; push is then switched off.</summary>
    bool IsConfigured { get; }
    string? PublicKey { get; }
    Task<PushSendResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default);
}

public interface IPushRepository
{
    Task<IReadOnlyList<PushSubscription>> GetSubscriptionsAsync(string userId, CancellationToken ct = default);
    Task<PushSubscription?> FindSubscriptionAsync(string endpoint, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetSubscribedUserIdsAsync(CancellationToken ct = default);
    Task AddSubscriptionAsync(PushSubscription subscription, CancellationToken ct = default);
    Task RemoveSubscriptionAsync(PushSubscription subscription, CancellationToken ct = default);

    Task<NotificationSettings?> GetSettingsAsync(string userId, CancellationToken ct = default);
    Task AddSettingsAsync(NotificationSettings settings, CancellationToken ct = default);

    Task<bool> HasLogAsync(string userId, DateOnly date, PushSlot slot, CancellationToken ct = default);
    Task AddLogAsync(NotificationLog log, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed record PublicKeyDto(string? Key);

/// <summary>Available = the server has VAPID keys. Devices = how many phones of this user receive notifications.</summary>
public sealed record PushSettingsDto(bool Available, bool Morning, bool Evening, int Devices);

public sealed record SavePushSettingsRequest(bool Morning, bool Evening);

public sealed record SubscribePushRequest(string Endpoint, string P256dh, string Auth);

public sealed record UnsubscribePushRequest(string Endpoint);

public sealed record TestPushResultDto(int Sent);

public sealed class PushService(IFamilyRepository families, IPushRepository push, IPushSender sender, IClock clock)
{
    public PublicKeyDto GetPublicKey() => new(sender.IsConfigured ? sender.PublicKey : null);

    public async Task<PushSettingsDto> GetSettingsAsync(string userId, CancellationToken ct = default)
    {
        await RequireMemberAsync(userId, ct);
        var settings = await push.GetSettingsAsync(userId, ct);
        var devices = (await push.GetSubscriptionsAsync(userId, ct)).Count;
        return new PushSettingsDto(sender.IsConfigured, settings?.Morning ?? true, settings?.Evening ?? true, devices);
    }

    public async Task<PushSettingsDto> SaveSettingsAsync(string userId, SavePushSettingsRequest r, CancellationToken ct = default)
    {
        await RequireMemberAsync(userId, ct);
        var settings = await push.GetSettingsAsync(userId, ct);
        if (settings is null) await push.AddSettingsAsync(NotificationSettings.Create(userId, r.Morning, r.Evening), ct);
        else settings.Set(r.Morning, r.Evening);
        await push.SaveChangesAsync(ct);
        return await GetSettingsAsync(userId, ct);
    }

    public async Task SubscribeAsync(string userId, SubscribePushRequest r, CancellationToken ct = default)
    {
        await RequireMemberAsync(userId, ct);
        if (!sender.IsConfigured) throw new DomainException("Notifikationer er ikke sat op på serveren endnu.");
        var existing = await push.FindSubscriptionAsync(r.Endpoint, ct);
        if (existing is null) await push.AddSubscriptionAsync(PushSubscription.Create(userId, r.Endpoint, r.P256dh, r.Auth, clock.UtcNow), ct);
        else existing.Rebind(userId, r.P256dh, r.Auth);
        await push.SaveChangesAsync(ct);
    }

    /// <summary>Removes the device. Only the owner can remove it; an unknown device is not an error.</summary>
    public async Task UnsubscribeAsync(string userId, UnsubscribePushRequest r, CancellationToken ct = default)
    {
        var existing = await push.FindSubscriptionAsync(r.Endpoint, ct);
        if (existing is null || existing.UserId != userId) return;
        await push.RemoveSubscriptionAsync(existing, ct);
        await push.SaveChangesAsync(ct);
    }

    /// <summary>Sends a test message to all of the user's devices.</summary>
    public async Task<TestPushResultDto> SendTestAsync(string userId, CancellationToken ct = default)
    {
        await RequireMemberAsync(userId, ct);
        if (!sender.IsConfigured) throw new DomainException("Notifikationer er ikke sat op på serveren endnu.");
        var message = new PushMessage("Vores familie", "Notifikationer virker 🎉", "/min-side");
        var sent = await PushDispatcher.SendToUserAsync(push, sender, userId, message, ct);
        if (sent == 0) throw new DomainException("Der er ingen telefon at sende til. Slå notifikationer til på denne telefon først.");
        return new TestPushResultDto(sent);
    }

    private async Task RequireMemberAsync(string userId, CancellationToken ct)
    {
        if (await families.GetByUserAsync(userId, ct) is null) throw new NotFoundException("Du er ikke medlem af en familie.");
    }
}

/// <summary>Builds the personal reminder for a person: what is left to do today. Returns null when there is nothing to remind about.</summary>
public sealed class DigestService(IFamilyRepository families, ChecklistService checklist, MealPlanService meals,
    HomeworkService homework, CleaningService cleaning, IClock clock)
{
    private const int MaxNamesShown = 3;

    public async Task<PushMessage?> BuildAsync(string userId, PushSlot slot, CancellationToken ct = default)
    {
        var family = await families.GetByUserAsync(userId, ct);
        var me = family?.FindMemberByUser(userId);
        if (me is null) return null;

        var today = FamilyClock.Today(clock);
        var lines = new List<string>();

        // Each part is optional: a missing right (or a hiccup in one source) must not stop the others.
        var entries = await TryAsync(async () => (await checklist.GetDayAsync(userId, today, null, ct)).Entries.Where(e => !e.Done).ToList()) ?? [];

        if (slot == PushSlot.Morning)
        {
            var meal = await TryAsync(async () => (await meals.GetAsync(userId, today, today, ct)).FirstOrDefault());
            if (meal is not null) lines.Add($"Aftensmad: {meal.Title}");
            AddList(lines, "Morgenrutine", entries.Where(e => e.Part == RoutineSlot.Morning));
            AddList(lines, "Opgaver", entries.Where(e => e.Kind == ChecklistKind.HouseholdTask));
        }
        else
        {
            AddList(lines, "Aftenrutine", entries.Where(e => e.Part == RoutineSlot.Evening));

            var from = new DateTimeOffset(today.AddDays(-7).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var to = new DateTimeOffset(today.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var open = await TryAsync(async () => (await homework.ListAsync(userId, from, to, me.Id, ct)).Where(h => !h.Done).ToList()) ?? [];
            if (open.Count > 0) lines.Add(open.Count == 1 ? "1 lektie mangler" : $"{open.Count} lektier mangler");

            var due = await TryAsync(async () => (await cleaning.ListAsync(userId, ct))
                .Where(c => c.Status != CleaningStatus.Upcoming && (c.AssigneeMemberId == me.Id || c.AssigneeMemberId is null)).ToList()) ?? [];
            if (due.Count > 0) AddNames(lines, "Rengøring", due.Select(c => c.Name).ToList());
        }

        if (lines.Count == 0) return null;
        var title = slot == PushSlot.Morning ? $"God morgen, {me.DisplayName}" : $"Godaften, {me.DisplayName}";
        return new PushMessage(title, string.Join("\n", lines), "/min-side");
    }

    private static void AddList(List<string> lines, string label, IEnumerable<ChecklistEntryDto> entries)
    {
        var names = entries.Select(e => e.Title).ToList();
        if (names.Count > 0) AddNames(lines, label, names);
    }

    private static void AddNames(List<string> lines, string label, List<string> names)
    {
        var shown = string.Join(", ", names.Take(MaxNamesShown));
        var more = names.Count > MaxNamesShown ? $" og {names.Count - MaxNamesShown} til" : "";
        lines.Add($"{label}: {shown}{more}");
    }

    private static async Task<T?> TryAsync<T>(Func<Task<T>> read)
    {
        try { return await read(); }
        catch (Exception ex) when (ex is ForbiddenException or NotFoundException or DomainException) { return default; }
    }
}

/// <summary>Runs from a timer: sends the reminder that is due, once per person and day.</summary>
public sealed class PushDispatcher(IPushRepository push, IPushSender sender, DigestService digest, IClock clock)
{
    /// <summary>Returns how many notifications were sent.</summary>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        if (!sender.IsConfigured) return 0;
        var local = TimeZoneInfo.ConvertTime(clock.UtcNow, FamilyZone);
        if (PushSchedule.DueSlot(TimeOnly.FromDateTime(local.DateTime)) is not { } slot) return 0;
        var today = FamilyClock.Today(clock);

        var sent = 0;
        foreach (var userId in await push.GetSubscribedUserIdsAsync(ct))
        {
            var settings = await push.GetSettingsAsync(userId, ct);
            if (settings is not null && !settings.Wants(slot)) continue;
            if (await push.HasLogAsync(userId, today, slot, ct)) continue;

            // Logged even when there is nothing to say, so we do not look again every minute.
            await push.AddLogAsync(NotificationLog.Create(userId, today, slot), ct);
            await push.SaveChangesAsync(ct);

            var message = await digest.BuildAsync(userId, slot, ct);
            if (message is not null) sent += await SendToUserAsync(push, sender, userId, message, ct);
        }
        return sent;
    }

    private static readonly TimeZoneInfo FamilyZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    /// <summary>Sends to every device of the user and forgets devices the push service says are gone. Returns how many were reached.</summary>
    internal static async Task<int> SendToUserAsync(IPushRepository push, IPushSender sender, string userId, PushMessage message, CancellationToken ct)
    {
        var sent = 0;
        foreach (var device in await push.GetSubscriptionsAsync(userId, ct))
        {
            var result = await sender.SendAsync(new PushTarget(device.Endpoint, device.P256dh, device.Auth), message, ct);
            if (result == PushSendResult.Sent) sent++;
            else if (result == PushSendResult.Expired) await push.RemoveSubscriptionAsync(device, ct);
        }
        await push.SaveChangesAsync(ct);
        return sent;
    }
}
