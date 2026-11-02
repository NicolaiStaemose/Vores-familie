using VoresFamilie.Web.Api;

namespace VoresFamilie.Web;

/// <summary>The few server calls the shopping list needs (the API in the app, a fake in tests).</summary>
public interface IShoppingRemote
{
    Task<List<ShoppingItemDto>> ListAsync();
    Task<ShoppingItemDto> AddAsync(SaveShoppingItemRequest request);
    Task SetBoughtAsync(Guid id, bool bought);
    Task DeleteAsync(Guid id);
}

public sealed class ApiShoppingRemote(IApiClient api) : IShoppingRemote
{
    public async Task<List<ShoppingItemDto>> ListAsync() => (await api.ListShoppingAsync()).ToList();
    public Task<ShoppingItemDto> AddAsync(SaveShoppingItemRequest request) => api.AddShoppingItemAsync(request);
    public Task SetBoughtAsync(Guid id, bool bought) => bought ? api.MarkShoppingBoughtAsync(id) : api.UnmarkShoppingBoughtAsync(id);
    public Task DeleteAsync(Guid id) => api.DeleteShoppingItemAsync(id);
}

public enum ChangeKind { Add, SetBought, Delete }

/// <summary>One change made while offline, waiting to be sent. For an Add, ItemId is a temporary id that only exists on this phone.</summary>
public sealed class PendingChange
{
    public ChangeKind Kind { get; set; }
    public Guid ItemId { get; set; }
    public string? Name { get; set; }
    public string? Quantity { get; set; }
    public ShoppingCategory Category { get; set; }
    public bool AutoCategory { get; set; }
    public bool Bought { get; set; }
}

public sealed record ShoppingView(List<ShoppingItemDto> Items, bool FromSavedCopy);

public sealed record FlushResult(int Sent, IReadOnlyList<(PendingChange Change, ApiException Error)> Failed);

/// <summary>
/// The shopping list that also works without a connection. The last list from the server is saved on the phone; ticking, adding and
/// deleting while offline is queued, shown straight away, and sent in order when the connection is back.
/// </summary>
public sealed class ShoppingOffline(IShoppingRemote remote, OfflineCache cache)
{
    private List<PendingChange>? _queue;
    private Task<FlushResult>? _flushTask;

    /// <summary>How many changes are waiting to be sent.</summary>
    public int Pending => _queue?.Count ?? 0;

    public event Action? Changed;

    // ---- Reading ----

    /// <summary>The list as it should be shown: from the server when possible, otherwise the saved copy, always with queued changes on top.</summary>
    public async Task<ShoppingView> GetAsync()
    {
        await EnsureQueueAsync();
        await FlushAsync();
        try
        {
            var items = await remote.ListAsync();
            await cache.SaveAsync(OfflineCache.Keys.Shopping, items);
            return new ShoppingView(Overlay(items), false);
        }
        catch (HttpRequestException)
        {
            var saved = await cache.LoadAsync<List<ShoppingItemDto>>(OfflineCache.Keys.Shopping) ?? [];
            return new ShoppingView(Overlay(saved), true);
        }
    }

    // ---- Changing ----

    /// <summary>Ticks an item (or takes the tick back). Throws the server's error when the change is refused.</summary>
    public Task SetBoughtAsync(Guid itemId, bool bought) =>
        ApplyAsync(new PendingChange { Kind = ChangeKind.SetBought, ItemId = itemId, Bought = bought });

    public Task AddAsync(SaveShoppingItemRequest request) =>
        ApplyAsync(new PendingChange
        {
            Kind = ChangeKind.Add, ItemId = Guid.NewGuid(), Name = request.Name, Quantity = request.Quantity,
            Category = request.Category, AutoCategory = request.AutoCategory,
        });

    public Task DeleteAsync(Guid itemId) => ApplyAsync(new PendingChange { Kind = ChangeKind.Delete, ItemId = itemId });

    private async Task ApplyAsync(PendingChange change)
    {
        await EnsureQueueAsync();
        Enqueue(change);
        await PersistAsync();
        Changed?.Invoke();

        var result = await FlushAsync();
        // A change that was sent and refused is the caller's error (a duplicate, a missing right ...).
        var refused = result.Failed.FirstOrDefault(f => f.Change == change);
        if (refused.Error is not null) throw refused.Error;
    }

    /// <summary>Puts a change in the queue, merging it with earlier changes to the same item so nothing is sent needlessly.</summary>
    private void Enqueue(PendingChange change)
    {
        var queue = _queue!;
        var pendingAdd = queue.FirstOrDefault(c => c.Kind == ChangeKind.Add && c.ItemId == change.ItemId);

        switch (change.Kind)
        {
            case ChangeKind.SetBought when pendingAdd is not null:
                pendingAdd.Bought = change.Bought; // the item has not reached the server yet
                return;
            case ChangeKind.Delete when pendingAdd is not null:
                queue.Remove(pendingAdd); // added and removed offline: the server never needs to know
                return;
            case ChangeKind.SetBought:
                queue.RemoveAll(c => c.Kind == ChangeKind.SetBought && c.ItemId == change.ItemId); // the last tick wins
                break;
            case ChangeKind.Delete:
                queue.RemoveAll(c => c.Kind == ChangeKind.SetBought && c.ItemId == change.ItemId);
                break;
        }
        queue.Add(change);
    }

    // ---- Sending ----

    /// <summary>Sends the queue in order. Stops (and keeps the rest) when there is still no connection.</summary>
    public async Task<FlushResult> FlushAsync()
    {
        await EnsureQueueAsync();
        // A second caller waits for the flush that is already running instead of showing a half-sent list.
        if (_flushTask is { IsCompleted: false }) return await _flushTask;
        if (_queue!.Count == 0) return new FlushResult(0, []);
        _flushTask = RunFlushAsync();
        return await _flushTask;
    }

    private async Task<FlushResult> RunFlushAsync()
    {
        var failed = new List<(PendingChange, ApiException)>();
        var sent = 0;
        try
        {
            while (_queue!.Count > 0)
            {
                var change = _queue[0];
                try
                {
                    await SendAsync(change);
                    sent++;
                }
                catch (HttpRequestException)
                {
                    break; // still offline: try again later
                }
                catch (ApiException ex)
                {
                    _queue.Remove(change); // the server said no; keeping it would block everything behind it
                    failed.Add((change, ex));
                }
                await PersistAsync();
            }
        }
        finally
        {
            await PersistAsync();
            Changed?.Invoke();
        }
        return new FlushResult(sent, failed);
    }

    private async Task SendAsync(PendingChange change)
    {
        switch (change.Kind)
        {
            case ChangeKind.Add:
                var created = await remote.AddAsync(new SaveShoppingItemRequest
                {
                    Name = change.Name, Quantity = change.Quantity, Category = change.Category, AutoCategory = change.AutoCategory,
                });
                if (change.Bought)
                {
                    // The item now exists: only the tick is left, so a retry cannot add it twice.
                    var index = _queue!.IndexOf(change);
                    _queue[index] = new PendingChange { Kind = ChangeKind.SetBought, ItemId = created.Id, Bought = true };
                    await PersistAsync();
                    await remote.SetBoughtAsync(created.Id, true);
                }
                _queue!.RemoveAll(c => c.Kind == ChangeKind.SetBought && c.ItemId == created.Id);
                if (_queue.Contains(change)) _queue.Remove(change);
                break;
            case ChangeKind.SetBought:
                await remote.SetBoughtAsync(change.ItemId, change.Bought);
                _queue!.Remove(change);
                break;
            case ChangeKind.Delete:
                await remote.DeleteAsync(change.ItemId);
                _queue!.Remove(change);
                break;
        }
    }

    // ---- Showing queued changes ----

    private List<ShoppingItemDto> Overlay(List<ShoppingItemDto> items)
    {
        foreach (var change in _queue!)
        {
            switch (change.Kind)
            {
                case ChangeKind.Add:
                    items.Add(new ShoppingItemDto
                    {
                        Id = change.ItemId, Name = change.Name, Quantity = change.Quantity,
                        // The department is worked out by the server; until then it is "Andet".
                        Category = change.AutoCategory ? ShoppingCategory.Other : change.Category,
                        IsBought = change.Bought,
                    });
                    break;
                case ChangeKind.SetBought:
                    var item = items.FirstOrDefault(i => i.Id == change.ItemId);
                    if (item is not null) item.IsBought = change.Bought;
                    break;
                case ChangeKind.Delete:
                    items.RemoveAll(i => i.Id == change.ItemId);
                    break;
            }
        }
        return items;
    }

    /// <summary>True for an item that was added offline and has not been sent yet.</summary>
    public bool IsNotSentYet(Guid itemId) => _queue?.Any(c => c.Kind == ChangeKind.Add && c.ItemId == itemId) == true;

    // ---- Saving the queue ----

    private async Task EnsureQueueAsync() =>
        _queue ??= await cache.LoadAsync<List<PendingChange>>(OfflineCache.Keys.ShoppingQueue) ?? [];

    private Task PersistAsync() => cache.SaveAsync(OfflineCache.Keys.ShoppingQueue, _queue ?? []);

    /// <summary>Forgets the queue (at log out the whole saved copy is removed).</summary>
    public void Reset()
    {
        _queue = null;
        Changed?.Invoke();
    }
}
