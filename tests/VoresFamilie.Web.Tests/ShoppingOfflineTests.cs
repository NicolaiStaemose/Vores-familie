using VoresFamilie.Web;
using VoresFamilie.Web.Api;

namespace VoresFamilie.Web.Tests;

internal sealed class MemStore : IKeyValueStore
{
    public Dictionary<string, string> Data { get; } = [];
    public Task<string?> GetAsync(string key) => Task.FromResult(Data.TryGetValue(key, out var v) ? v : null);
    public Task SetAsync(string key, string value) { Data[key] = value; return Task.CompletedTask; }
    public Task RemoveAsync(string key) { Data.Remove(key); return Task.CompletedTask; }
    public Task ClearAsync() { Data.Clear(); return Task.CompletedTask; }
}

/// <summary>A server with a shopping list that can be "unplugged".</summary>
internal sealed class FakeRemote : IShoppingRemote
{
    public bool Offline { get; set; }
    public List<ShoppingItemDto> Items { get; } = [];
    public List<string> Calls { get; } = [];
    public HashSet<string> Refuse { get; } = [];

    private void Check(string call)
    {
        if (Offline) throw new HttpRequestException("no connection");
        Calls.Add(call);
    }

    public Task<List<ShoppingItemDto>> ListAsync()
    {
        Check("list");
        // A copy, like a real response.
        return Task.FromResult(Items.Select(i => new ShoppingItemDto
        {
            Id = i.Id, Name = i.Name, Quantity = i.Quantity, Category = i.Category, IsBought = i.IsBought,
        }).ToList());
    }

    public Task<ShoppingItemDto> AddAsync(SaveShoppingItemRequest request)
    {
        Check("add " + request.Name);
        if (Refuse.Contains(request.Name)) throw new ApiException("refused", 400, "", new Dictionary<string, IEnumerable<string>>(), null!);
        var item = new ShoppingItemDto { Id = Guid.NewGuid(), Name = request.Name, Quantity = request.Quantity, Category = request.Category };
        Items.Add(item);
        return Task.FromResult(item);
    }

    public Task SetBoughtAsync(Guid id, bool bought)
    {
        Check((bought ? "buy " : "unbuy ") + Items.FirstOrDefault(i => i.Id == id)?.Name);
        Items.First(i => i.Id == id).IsBought = bought;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        Check("delete " + Items.FirstOrDefault(i => i.Id == id)?.Name);
        Items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }

    public ShoppingItemDto Seed(string name, bool bought = false)
    {
        var item = new ShoppingItemDto { Id = Guid.NewGuid(), Name = name, Category = ShoppingCategory.Other, IsBought = bought };
        Items.Add(item);
        return item;
    }
}

public class ShoppingOfflineTests
{
    private readonly MemStore _store = new();
    private readonly FakeRemote _remote = new();

    private ShoppingOffline NewService() => new(_remote, new OfflineCache(_store));

    private static SaveShoppingItemRequest Item(string name) => new() { Name = name, Category = ShoppingCategory.Other, AutoCategory = true };

    [Fact]
    public async Task Online_changes_go_straight_to_the_server()
    {
        var milk = _remote.Seed("Mælk");
        var shop = NewService();

        await shop.SetBoughtAsync(milk.Id, true);
        await shop.AddAsync(Item("Æg"));

        Assert.Equal(["buy Mælk", "add Æg"], _remote.Calls);
        Assert.Equal(0, shop.Pending);
        Assert.True(_remote.Items.Single(i => i.Name == "Mælk").IsBought);
    }

    [Fact]
    public async Task Offline_the_last_list_is_shown_from_the_saved_copy()
    {
        _remote.Seed("Mælk");
        var shop = NewService();
        Assert.False((await shop.GetAsync()).FromSavedCopy);

        _remote.Offline = true;
        var view = await NewService().GetAsync(); // a fresh start of the app, still without connection
        Assert.True(view.FromSavedCopy);
        Assert.Equal(["Mælk"], view.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Offline_without_a_saved_copy_gives_an_empty_list()
    {
        _remote.Offline = true;
        var view = await NewService().GetAsync();
        Assert.True(view.FromSavedCopy);
        Assert.Empty(view.Items);
    }

    [Fact]
    public async Task Changes_made_offline_show_at_once_and_are_sent_when_the_connection_is_back()
    {
        var milk = _remote.Seed("Mælk");
        var bread = _remote.Seed("Rugbrød");
        var shop = NewService();
        await shop.GetAsync();

        _remote.Offline = true;
        await shop.SetBoughtAsync(milk.Id, true);
        await shop.AddAsync(Item("Æg"));
        await shop.DeleteAsync(bread.Id);

        Assert.Equal(3, shop.Pending);
        var view = await shop.GetAsync();
        Assert.Equal(["Mælk", "Æg"], view.Items.Select(i => i.Name));
        Assert.True(view.Items.Single(i => i.Name == "Mælk").IsBought);
        Assert.DoesNotContain(_remote.Calls, c => c != "list");

        _remote.Offline = false;
        var result = await shop.FlushAsync();

        Assert.Equal(3, result.Sent);
        Assert.Equal(0, shop.Pending);
        Assert.Equal(["Mælk", "Æg"], _remote.Items.Select(i => i.Name));
        Assert.True(_remote.Items.Single(i => i.Name == "Mælk").IsBought);
    }

    [Fact]
    public async Task The_queue_survives_a_restart_of_the_app()
    {
        var milk = _remote.Seed("Mælk");
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;
        await shop.SetBoughtAsync(milk.Id, true);

        var afterRestart = NewService();
        Assert.Equal(1, (await afterRestart.GetAsync()).Items.Count(i => i.IsBought));
        Assert.Equal(1, afterRestart.Pending);

        _remote.Offline = false;
        await afterRestart.FlushAsync();
        Assert.True(_remote.Items.Single().IsBought);
    }

    [Fact]
    public async Task Ticking_back_and_forth_offline_sends_only_the_last_tick()
    {
        var milk = _remote.Seed("Mælk");
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;

        await shop.SetBoughtAsync(milk.Id, true);
        await shop.SetBoughtAsync(milk.Id, false);
        await shop.SetBoughtAsync(milk.Id, true);
        Assert.Equal(1, shop.Pending);

        _remote.Offline = false;
        await shop.FlushAsync();
        Assert.Equal(["buy Mælk"], _remote.Calls.Where(c => c != "list"));
    }

    [Fact]
    public async Task An_item_added_and_deleted_offline_never_reaches_the_server()
    {
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;
        await shop.AddAsync(Item("Æg"));
        var temp = (await shop.GetAsync()).Items.Single();
        Assert.True(shop.IsNotSentYet(temp.Id));

        await shop.DeleteAsync(temp.Id);
        Assert.Equal(0, shop.Pending);

        _remote.Offline = false;
        await shop.FlushAsync();
        Assert.Empty(_remote.Items);
    }

    [Fact]
    public async Task An_item_added_and_ticked_offline_is_added_as_bought()
    {
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;
        await shop.AddAsync(Item("Æg"));
        var temp = (await shop.GetAsync()).Items.Single();
        await shop.SetBoughtAsync(temp.Id, true);
        Assert.Equal(1, shop.Pending);

        _remote.Offline = false;
        await shop.FlushAsync();
        var sent = Assert.Single(_remote.Items);
        Assert.True(sent.IsBought);
        Assert.Equal(["add Æg", "buy Æg"], _remote.Calls.Where(c => c != "list"));
    }

    [Fact]
    public async Task Still_offline_keeps_the_queue_in_order()
    {
        var milk = _remote.Seed("Mælk");
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;
        await shop.SetBoughtAsync(milk.Id, true);
        await shop.AddAsync(Item("Æg"));

        var result = await shop.FlushAsync();
        Assert.Equal(0, result.Sent);
        Assert.Equal(2, shop.Pending);
    }

    [Fact]
    public async Task A_change_the_server_refuses_is_dropped_and_does_not_block_the_rest()
    {
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;
        await shop.AddAsync(Item("Mælk"));
        await shop.AddAsync(Item("Æg"));
        _remote.Refuse.Add("Mælk"); // already on the list when it arrives

        _remote.Offline = false;
        var result = await shop.FlushAsync();

        Assert.Equal(1, result.Sent);
        Assert.Single(result.Failed);
        Assert.Equal(0, shop.Pending);
        Assert.Equal(["Æg"], _remote.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task A_refused_change_made_online_is_reported_to_the_caller()
    {
        var shop = NewService();
        _remote.Refuse.Add("Mælk");
        await Assert.ThrowsAsync<ApiException>(() => shop.AddAsync(Item("Mælk")));
        Assert.Equal(0, shop.Pending);
    }

    [Fact]
    public async Task Log_out_removes_the_saved_copy_and_the_queue()
    {
        var milk = _remote.Seed("Mælk");
        var shop = NewService();
        await shop.GetAsync();
        _remote.Offline = true;
        await shop.SetBoughtAsync(milk.Id, true);
        Assert.NotEmpty(_store.Data);

        await new OfflineCache(_store).ClearAsync();
        shop.Reset();

        Assert.Empty(_store.Data);
        Assert.Equal(0, shop.Pending);
        Assert.Empty((await shop.GetAsync()).Items);
    }
}

public class OfflineCacheTests
{
    [Fact]
    public async Task Saves_and_loads_dtos_with_enums_and_dates()
    {
        var cache = new OfflineCache(new MemStore());
        var item = new ShoppingItemDto { Id = Guid.NewGuid(), Name = "Mælk", Category = ShoppingCategory.Dairy, IsBought = true };
        await cache.SaveAsync("k", new List<ShoppingItemDto> { item });

        var loaded = (await cache.LoadAsync<List<ShoppingItemDto>>("k"))!.Single();
        Assert.Equal((item.Id, "Mælk", ShoppingCategory.Dairy, true), (loaded.Id, loaded.Name, loaded.Category, loaded.IsBought));
    }

    [Fact]
    public async Task A_damaged_copy_counts_as_no_copy()
    {
        var store = new MemStore();
        store.Data["k"] = "{ not json";
        Assert.Null(await new OfflineCache(store).LoadAsync<List<ShoppingItemDto>>("k"));
    }
}
