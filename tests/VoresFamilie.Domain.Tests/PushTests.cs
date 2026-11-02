using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class PushScheduleTests
{
    [Theory]
    [InlineData(6, 59, null)]
    [InlineData(7, 0, PushSlot.Morning)]
    [InlineData(7, 59, PushSlot.Morning)]
    [InlineData(8, 0, null)]
    [InlineData(10, 0, null)]
    [InlineData(19, 0, PushSlot.Evening)]
    [InlineData(19, 30, PushSlot.Evening)]
    [InlineData(20, 0, null)]
    public void Slot_is_only_due_in_the_hour_after_the_planned_time(int hour, int minute, PushSlot? expected)
    {
        Assert.Equal(expected, PushSchedule.DueSlot(new TimeOnly(hour, minute)));
    }
}

public class PushSubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_valid_subscription_is_created()
    {
        var s = PushSubscription.Create("u1", "https://push.example.com/abc", "key", "auth", Now);
        Assert.Equal(("u1", "https://push.example.com/abc"), (s.UserId, s.Endpoint));
    }

    [Theory]
    [InlineData("", "k", "a")]
    [InlineData("http://push.example.com/abc", "k", "a")]
    [InlineData("not a url", "k", "a")]
    [InlineData("https://push.example.com/abc", "", "a")]
    [InlineData("https://push.example.com/abc", "k", " ")]
    public void Invalid_subscriptions_are_refused(string endpoint, string key, string auth)
    {
        Assert.Throws<DomainException>(() => PushSubscription.Create("u1", endpoint, key, auth, Now));
    }

    [Fact]
    public void Rebinding_moves_the_device_to_another_user_with_new_keys()
    {
        var s = PushSubscription.Create("u1", "https://push.example.com/abc", "k", "a", Now);
        s.Rebind("u2", "k2", "a2");
        Assert.Equal(("u2", "k2", "a2"), (s.UserId, s.P256dh, s.Auth));
    }
}

public class NotificationSettingsTests
{
    [Fact]
    public void Slots_can_be_switched_off_one_by_one()
    {
        var s = NotificationSettings.Create("u1", true, true);
        Assert.True(s.Wants(PushSlot.Morning) && s.Wants(PushSlot.Evening));
        s.Set(false, true);
        Assert.False(s.Wants(PushSlot.Morning));
        Assert.True(s.Wants(PushSlot.Evening));
    }
}
