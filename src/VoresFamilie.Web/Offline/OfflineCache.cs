using System.Text.Json;
using Microsoft.JSInterop;

namespace VoresFamilie.Web;

/// <summary>A tiny string store. The browser's localStorage in the app; a dictionary in tests.</summary>
public interface IKeyValueStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
    /// <summary>Removes everything this app has saved.</summary>
    Task ClearAsync();
}

/// <summary>localStorage with a prefix, so we only ever touch our own keys. Every call tolerates a browser that blocks storage.</summary>
public sealed class BrowserStorage(IJSRuntime js) : IKeyValueStore
{
    private const string Prefix = "vf:";

    public async Task<string?> GetAsync(string key)
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", Prefix + key); }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { return null; }
    }

    public async Task SetAsync(string key, string value)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", Prefix + key, value); }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { /* full or blocked: the app works without a saved copy */ }
    }

    public async Task RemoveAsync(string key)
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", Prefix + key); }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }

    public async Task ClearAsync()
    {
        try { await js.InvokeVoidAsync("pwa.clearStorage", Prefix); }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }
}

/// <summary>
/// The app's saved copy of what it last got from the server (who is signed in, the shopping list, "Min side"), used when there is
/// no connection. Everything is removed on log out.
/// </summary>
public sealed class OfflineCache(IKeyValueStore store)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync<T>(string key, T value) => await store.SetAsync(key, JsonSerializer.Serialize(value, Options));

    public async Task<T?> LoadAsync<T>(string key)
    {
        var json = await store.GetAsync(key);
        if (string.IsNullOrEmpty(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch (JsonException) { return default; } // an old or damaged copy is the same as no copy
    }

    public Task RemoveAsync(string key) => store.RemoveAsync(key);
    public Task ClearAsync() => store.ClearAsync();

    public static class Keys
    {
        public const string Me = "me";
        public const string Shopping = "shopping";
        public const string ShoppingQueue = "shopping-queue";
        public const string MyPage = "my-page";
    }
}
