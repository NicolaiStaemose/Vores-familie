namespace VoresFamilie.Domain;

/// <summary>The two daily reminders.</summary>
public enum PushSlot { Morning, Evening }

/// <summary>One phone (browser) that has agreed to receive notifications. A user can have several.</summary>
public sealed class PushSubscription
{
    private PushSubscription() { }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = "";
    /// <summary>The push service's address for this device; it identifies the subscription.</summary>
    public string Endpoint { get; private set; } = "";
    public string P256dh { get; private set; } = "";
    public string Auth { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Endpoints are indexed, so they have a size limit (real ones are a few hundred characters).</summary>
    public const int MaxEndpointLength = 450;

    public static PushSubscription Create(string userId, string endpoint, string p256dh, string auth, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Bruger mangler.");
        if (endpoint is null || endpoint.Length > MaxEndpointLength || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new DomainException("Ugyldig adresse til notifikationer.");
        if (string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth)) throw new DomainException("Nøglerne til notifikationer mangler.");
        return new PushSubscription { Id = Guid.NewGuid(), UserId = userId, Endpoint = endpoint, P256dh = p256dh, Auth = auth, CreatedAt = now };
    }

    /// <summary>The same device can be re-subscribed with fresh keys (and may now belong to another user).</summary>
    public void Rebind(string userId, string p256dh, string auth)
    {
        if (string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth)) throw new DomainException("Nøglerne til notifikationer mangler.");
        UserId = userId;
        P256dh = p256dh;
        Auth = auth;
    }
}

/// <summary>Which reminders a user wants. Both are on until the user turns them off.</summary>
public sealed class NotificationSettings
{
    private NotificationSettings() { }

    public string UserId { get; private set; } = "";
    public bool Morning { get; private set; } = true;
    public bool Evening { get; private set; } = true;

    public static NotificationSettings Create(string userId, bool morning, bool evening) =>
        new() { UserId = userId, Morning = morning, Evening = evening };

    public void Set(bool morning, bool evening)
    {
        Morning = morning;
        Evening = evening;
    }

    public bool Wants(PushSlot slot) => slot == PushSlot.Morning ? Morning : Evening;
}

/// <summary>Remembers that a user has been handled for a slot on a day, so a reminder is never sent twice.</summary>
public sealed class NotificationLog
{
    private NotificationLog() { }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = "";
    public DateOnly Date { get; private set; }
    public PushSlot Slot { get; private set; }

    public static NotificationLog Create(string userId, DateOnly date, PushSlot slot) =>
        new() { Id = Guid.NewGuid(), UserId = userId, Date = date, Slot = slot };
}

/// <summary>When the reminders go out (Copenhagen time).</summary>
public static class PushSchedule
{
    public static readonly TimeOnly MorningAt = new(7, 0);
    public static readonly TimeOnly EveningAt = new(19, 0);
    /// <summary>How long after the planned time a reminder is still sent (covers a short restart, never a reminder at 10:00).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(60);

    public static PushSlot? DueSlot(TimeOnly nowLocal)
    {
        if (InWindow(nowLocal, MorningAt)) return PushSlot.Morning;
        if (InWindow(nowLocal, EveningAt)) return PushSlot.Evening;
        return null;
    }

    private static bool InWindow(TimeOnly now, TimeOnly start) => now >= start && now < start.Add(Window);
}
