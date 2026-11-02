using Microsoft.JSInterop;

namespace VoresFamilie.Web;

/// <summary>The keys the browser made for one push subscription (sent to the server so it can reach this phone).</summary>
public sealed record PushKeys(string Endpoint, string P256dh, string Auth);

/// <summary>What the browser told us when we started listening.</summary>
public sealed record PwaState(bool Online, bool CanInstall, bool Standalone);

/// <summary>Installing the app, new versions, online/offline and the browser's side of push notifications (see wwwroot/pwa.js).</summary>
public sealed class Pwa(IJSRuntime js) : IAsyncDisposable
{
    private DotNetObjectReference<Pwa>? _ref;
    private bool _started;

    public bool Online { get; private set; } = true;
    public bool CanInstall { get; private set; }
    public bool IsInstalled { get; private set; }
    public bool UpdateReady { get; private set; }

    /// <summary>Raised for every change of the properties above.</summary>
    public event Action? Changed;
    /// <summary>Raised when the connection comes back.</summary>
    public event Action? CameOnline;

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        _ref = DotNetObjectReference.Create(this);
        try
        {
            var state = await js.InvokeAsync<PwaState>("pwa.init", _ref);
            Online = state.Online;
            CanInstall = state.CanInstall;
            IsInstalled = state.Standalone;
        }
        catch (JSException) { /* an old browser without service workers: the app still works online */ }
        Changed?.Invoke();
    }

    [JSInvokable] public void OnInstallAvailable(bool available) { CanInstall = available; Changed?.Invoke(); }
    [JSInvokable] public void OnUpdateReady() { UpdateReady = true; Changed?.Invoke(); }

    [JSInvokable]
    public void OnOnlineChanged(bool online)
    {
        var cameBack = online && !Online;
        Online = online;
        Changed?.Invoke();
        if (cameBack) CameOnline?.Invoke();
    }

    public async Task<bool> InstallAsync() => await js.InvokeAsync<bool>("pwa.install");
    public async Task ApplyUpdateAsync() => await js.InvokeVoidAsync("pwa.applyUpdate");

    // ---- Push (browser side) ----

    public async Task<bool> PushSupportedAsync()
    {
        try { return await js.InvokeAsync<bool>("pwa.pushSupported"); }
        catch (JSException) { return false; }
    }

    /// <summary>"granted", "denied" or "default" (not asked yet).</summary>
    public async Task<string> PushPermissionAsync() => await js.InvokeAsync<string>("pwa.pushPermission");

    /// <summary>The endpoint of this phone's subscription, or null when it is not subscribed.</summary>
    public async Task<string?> PushEndpointAsync()
    {
        try { return await js.InvokeAsync<string?>("pwa.pushEndpoint"); }
        catch (JSException) { return null; }
    }

    /// <summary>Asks the user for permission and subscribes. Null when permission was refused.</summary>
    public async Task<PushKeys?> PushSubscribeAsync(string publicKey) => await js.InvokeAsync<PushKeys?>("pwa.pushSubscribe", publicKey);

    /// <summary>Cancels this phone's subscription. Returns its endpoint, or null when there was none.</summary>
    public async Task<string?> PushUnsubscribeAsync() => await js.InvokeAsync<string?>("pwa.pushUnsubscribe");

    public ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        return ValueTask.CompletedTask;
    }
}
