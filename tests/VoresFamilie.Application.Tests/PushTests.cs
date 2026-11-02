using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Application.Tests;

internal sealed class MemPush : IPushRepository
{
    public List<PushSubscription> Subscriptions { get; } = [];
    public List<NotificationSettings> Settings { get; } = [];
    public List<NotificationLog> Logs { get; } = [];
    public Task<IReadOnlyList<PushSubscription>> GetSubscriptionsAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PushSubscription>>(Subscriptions.Where(s => s.UserId == userId).ToList());
    public Task<PushSubscription?> FindSubscriptionAsync(string endpoint, CancellationToken ct = default) =>
        Task.FromResult(Subscriptions.FirstOrDefault(s => s.Endpoint == endpoint));
    public Task<IReadOnlyList<string>> GetSubscribedUserIdsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Subscriptions.Select(s => s.UserId).Distinct().ToList());
    public Task AddSubscriptionAsync(PushSubscription s, CancellationToken ct = default) { Subscriptions.Add(s); return Task.CompletedTask; }
    public Task RemoveSubscriptionAsync(PushSubscription s, CancellationToken ct = default) { Subscriptions.Remove(s); return Task.CompletedTask; }
    public Task<NotificationSettings?> GetSettingsAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(Settings.FirstOrDefault(s => s.UserId == userId));
    public Task AddSettingsAsync(NotificationSettings s, CancellationToken ct = default) { Settings.Add(s); return Task.CompletedTask; }
    public Task<bool> HasLogAsync(string userId, DateOnly date, PushSlot slot, CancellationToken ct = default) =>
        Task.FromResult(Logs.Any(l => l.UserId == userId && l.Date == date && l.Slot == slot));
    public Task AddLogAsync(NotificationLog log, CancellationToken ct = default) { Logs.Add(log); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakePushSender : IPushSender
{
    public bool IsConfigured { get; set; } = true;
    public string? PublicKey => "public-key";
    public List<(PushTarget Target, PushMessage Message)> Sent { get; } = [];
    public HashSet<string> Gone { get; } = [];
    public Task<PushSendResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default)
    {
        if (Gone.Contains(target.Endpoint)) return Task.FromResult(PushSendResult.Expired);
        Sent.Add((target, message));
        return Task.FromResult(PushSendResult.Sent);
    }
}

/// <summary>Family with an admin and two children. Monday 19 Oct 2026 is "today"; the clock starts at 07:00 Copenhagen time.</summary>
internal sealed class PushEnv
{
    public Env Base { get; } = new();
    public MemChecklist Checklist { get; } = new();
    public MemMeals Meals { get; } = new();
    public MemShopping ShoppingRepo { get; } = new();
    public MemCleaning CleaningRepo { get; } = new();
    public MemPush Push { get; } = new();
    public FakePushSender Sender { get; } = new();
    public ChecklistService ChecklistService { get; }
    public MealPlanService MealPlan { get; }
    public CleaningService Cleaning { get; }
    public PushService Service { get; }
    public DigestService Digest { get; }
    public PushDispatcher Dispatcher { get; }

    public const string Admin = Env.AdminUser, Child = Env.ChildUser, Sibling = Env.SiblingUser;
    public Guid ChildId => Base.Child.Id;
    public static readonly DateOnly Monday = new(2026, 10, 19);

    public PushEnv()
    {
        ChecklistService = new ChecklistService(Base.Families, Checklist, Base.Time);
        var shopping = new ShoppingService(Base.Families, ShoppingRepo, Base.Time);
        MealPlan = new MealPlanService(Base.Families, Meals, shopping);
        Cleaning = new CleaningService(Base.Families, CleaningRepo, Base.Time);
        Service = new PushService(Base.Families, Push, Sender, Base.Time);
        Digest = new DigestService(Base.Families, ChecklistService, MealPlan, Base.Homework, Cleaning, Base.Time);
        Dispatcher = new PushDispatcher(Push, Sender, Digest, Base.Time);
        GoTo(Monday, 7, 0);
    }

    /// <summary>Sets the clock to a Copenhagen local time.</summary>
    public void GoTo(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        Base.Time.UtcNow = new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public Task SubscribeAsync(string user) =>
        Service.SubscribeAsync(user, new SubscribePushRequest("https://push.example.com/" + user, "key", "auth"));

    public Task<ChecklistItemDto> AddRoutine(Guid member, RoutineSlot part, string title) =>
        ChecklistService.CreateItemAsync(Admin, new SaveChecklistItemRequest(ChecklistKind.Routine, part, member, title, (int)DaysOfWeek.All));
}

public class PushServiceTests
{
    [Fact]
    public async Task Subscribing_adds_the_device_and_the_settings_default_to_on()
    {
        var env = new PushEnv();
        await env.SubscribeAsync(PushEnv.Child);
        var settings = await env.Service.GetSettingsAsync(PushEnv.Child);
        Assert.Equal(new PushSettingsDto(true, true, true, 1), settings);
    }

    [Fact]
    public async Task The_same_device_is_not_added_twice_and_can_move_to_another_user()
    {
        var env = new PushEnv();
        await env.Service.SubscribeAsync(PushEnv.Child, new SubscribePushRequest("https://push.example.com/x", "k", "a"));
        await env.Service.SubscribeAsync(PushEnv.Admin, new SubscribePushRequest("https://push.example.com/x", "k2", "a2"));
        var device = Assert.Single(env.Push.Subscriptions);
        Assert.Equal(PushEnv.Admin, device.UserId);
    }

    [Fact]
    public async Task Only_the_owner_can_remove_a_device()
    {
        var env = new PushEnv();
        await env.SubscribeAsync(PushEnv.Child);
        var endpoint = "https://push.example.com/" + PushEnv.Child;
        await env.Service.UnsubscribeAsync(PushEnv.Admin, new UnsubscribePushRequest(endpoint));
        Assert.Single(env.Push.Subscriptions);
        await env.Service.UnsubscribeAsync(PushEnv.Child, new UnsubscribePushRequest(endpoint));
        Assert.Empty(env.Push.Subscriptions);
    }

    [Fact]
    public async Task Without_server_keys_push_is_unavailable()
    {
        var env = new PushEnv();
        env.Sender.IsConfigured = false;
        Assert.Null(env.Service.GetPublicKey().Key);
        Assert.False((await env.Service.GetSettingsAsync(PushEnv.Child)).Available);
        await Assert.ThrowsAsync<DomainException>(() => env.SubscribeAsync(PushEnv.Child));
    }

    [Fact]
    public async Task Settings_are_saved_per_user()
    {
        var env = new PushEnv();
        var saved = await env.Service.SaveSettingsAsync(PushEnv.Child, new SavePushSettingsRequest(false, true));
        Assert.False(saved.Morning);
        Assert.True((await env.Service.GetSettingsAsync(PushEnv.Admin)).Morning);
    }

    [Fact]
    public async Task Test_message_reaches_every_device_and_needs_a_device()
    {
        var env = new PushEnv();
        await Assert.ThrowsAsync<DomainException>(() => env.Service.SendTestAsync(PushEnv.Child));
        await env.Service.SubscribeAsync(PushEnv.Child, new SubscribePushRequest("https://push.example.com/a", "k", "a"));
        await env.Service.SubscribeAsync(PushEnv.Child, new SubscribePushRequest("https://push.example.com/b", "k", "a"));
        Assert.Equal(2, (await env.Service.SendTestAsync(PushEnv.Child)).Sent);
    }
}

public class DigestServiceTests
{
    [Fact]
    public async Task Morning_lists_dinner_and_what_is_left_of_the_morning_routine_and_tasks()
    {
        var env = new PushEnv();
        await env.AddRoutine(env.ChildId, RoutineSlot.Morning, "Børst tænder");
        await env.AddRoutine(env.ChildId, RoutineSlot.Morning, "Spis morgenmad");
        await env.AddRoutine(env.ChildId, RoutineSlot.Evening, "Pak skoletaske");
        await env.ChecklistService.CreateItemAsync(PushEnv.Admin, new SaveChecklistItemRequest(ChecklistKind.HouseholdTask, RoutineSlot.None, null, "Rydde køkken", 127));
        await env.MealPlan.SetAsync(PushEnv.Admin, PushEnv.Monday, new SaveMealRequest("Lasagne", null, null, []));

        var message = await env.Digest.BuildAsync(PushEnv.Child, PushSlot.Morning);

        Assert.NotNull(message);
        Assert.Equal("God morgen, Emma", message!.Title);
        Assert.Equal("Aftensmad: Lasagne\nMorgenrutine: Børst tænder, Spis morgenmad\nOpgaver: Rydde køkken", message.Body);
        Assert.Equal("/min-side", message.Url);
    }

    [Fact]
    public async Task Ticked_items_are_left_out_and_nothing_to_say_gives_no_message()
    {
        var env = new PushEnv();
        var item = await env.AddRoutine(env.ChildId, RoutineSlot.Morning, "Børst tænder");
        await env.ChecklistService.SetDoneAsync(PushEnv.Child, item.Id, PushEnv.Monday, true);
        Assert.Null(await env.Digest.BuildAsync(PushEnv.Child, PushSlot.Morning));
    }

    [Fact]
    public async Task Long_lists_are_shortened()
    {
        var env = new PushEnv();
        foreach (var title in new[] { "A", "B", "C", "D", "E" }) await env.AddRoutine(env.ChildId, RoutineSlot.Evening, title);
        var message = await env.Digest.BuildAsync(PushEnv.Child, PushSlot.Evening);
        Assert.Equal("Aftenrutine: A, B, C og 2 til", message!.Body);
    }

    [Fact]
    public async Task Evening_lists_routine_and_overdue_cleaning_that_is_mine_or_nobodys()
    {
        var env = new PushEnv();
        await env.AddRoutine(env.ChildId, RoutineSlot.Evening, "Pak skoletaske");
        await env.Cleaning.CreateAsync(PushEnv.Admin, new SaveCleaningRequest("Støvsug stuen", "Stue", 7, null));
        await env.Cleaning.CreateAsync(PushEnv.Admin, new SaveCleaningRequest("Noahs værelse", "Værelse", 7, env.Base.Sibling.Id));

        var message = await env.Digest.BuildAsync(PushEnv.Child, PushSlot.Evening);

        Assert.Equal("Godaften, Emma", message!.Title);
        Assert.Equal("Aftenrutine: Pak skoletaske\nRengøring: Støvsug stuen", message.Body);
    }

    [Fact]
    public async Task An_area_the_user_may_not_see_is_skipped()
    {
        var env = new PushEnv();
        await env.MealPlan.SetAsync(PushEnv.Admin, PushEnv.Monday, new SaveMealRequest("Lasagne", null, null, []));
        env.Base.Family.SetPermission(env.ChildId, Area.MealPlan, AccessLevel.None);
        await env.AddRoutine(env.ChildId, RoutineSlot.Morning, "Børst tænder");
        var message = await env.Digest.BuildAsync(PushEnv.Child, PushSlot.Morning);
        Assert.Equal("Morgenrutine: Børst tænder", message!.Body);
    }
}

public class PushDispatcherTests
{
    private static async Task<PushEnv> WithChildReminderAsync()
    {
        var env = new PushEnv();
        await env.AddRoutine(env.ChildId, RoutineSlot.Morning, "Børst tænder");
        await env.SubscribeAsync(PushEnv.Child);
        return env;
    }

    [Fact]
    public async Task Sends_the_morning_reminder_once_per_day()
    {
        var env = await WithChildReminderAsync();
        Assert.Equal(1, await env.Dispatcher.RunAsync());
        env.GoTo(PushEnv.Monday, 7, 30);
        Assert.Equal(0, await env.Dispatcher.RunAsync());
        Assert.Single(env.Sender.Sent);

        env.GoTo(PushEnv.Monday.AddDays(1), 7, 0);
        Assert.Equal(1, await env.Dispatcher.RunAsync()); // a new day
    }

    [Fact]
    public async Task Sends_nothing_outside_the_two_windows()
    {
        var env = await WithChildReminderAsync();
        env.GoTo(PushEnv.Monday, 12, 0);
        Assert.Equal(0, await env.Dispatcher.RunAsync());
        env.GoTo(PushEnv.Monday, 6, 59);
        Assert.Equal(0, await env.Dispatcher.RunAsync());
    }

    [Fact]
    public async Task A_slot_the_user_switched_off_is_skipped()
    {
        var env = await WithChildReminderAsync();
        await env.Service.SaveSettingsAsync(PushEnv.Child, new SavePushSettingsRequest(false, true));
        Assert.Equal(0, await env.Dispatcher.RunAsync());
    }

    [Fact]
    public async Task Does_not_look_again_when_there_was_nothing_to_remind_about()
    {
        var env = new PushEnv();
        await env.SubscribeAsync(PushEnv.Child);
        Assert.Equal(0, await env.Dispatcher.RunAsync());
        // Something shows up a minute later: the reminder for this morning is already settled.
        await env.AddRoutine(env.ChildId, RoutineSlot.Morning, "Børst tænder");
        env.GoTo(PushEnv.Monday, 7, 1);
        Assert.Equal(0, await env.Dispatcher.RunAsync());
    }

    [Fact]
    public async Task A_gone_device_is_forgotten_and_the_others_still_get_the_message()
    {
        var env = await WithChildReminderAsync();
        await env.Service.SubscribeAsync(PushEnv.Child, new SubscribePushRequest("https://push.example.com/old", "k", "a"));
        env.Sender.Gone.Add("https://push.example.com/old");

        Assert.Equal(1, await env.Dispatcher.RunAsync());
        Assert.DoesNotContain(env.Push.Subscriptions, s => s.Endpoint.EndsWith("/old"));
    }

    [Fact]
    public async Task Each_person_gets_their_own_message()
    {
        var env = await WithChildReminderAsync();
        await env.AddRoutine(env.Base.Sibling.Id, RoutineSlot.Morning, "Tag medicin");
        await env.SubscribeAsync(PushEnv.Sibling);

        Assert.Equal(2, await env.Dispatcher.RunAsync());
        Assert.Contains(env.Sender.Sent, s => s.Message.Body == "Morgenrutine: Børst tænder");
        Assert.Contains(env.Sender.Sent, s => s.Message.Body == "Morgenrutine: Tag medicin");
    }

    [Fact]
    public async Task Does_nothing_without_server_keys()
    {
        var env = await WithChildReminderAsync();
        env.Sender.IsConfigured = false;
        Assert.Equal(0, await env.Dispatcher.RunAsync());
        Assert.Empty(env.Push.Logs);
    }
}
