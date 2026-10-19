using Microsoft.AspNetCore.Components;

namespace VoresFamilie.Web;

/// <summary>Page base class that re-renders when the signed-in user changes and loads data once signed in.</summary>
public abstract class SessionComponent : ComponentBase, IDisposable
{
    private bool _wasSignedIn;

    [Inject] protected Session Session { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Session.Changed += OnSessionChanged;
        _wasSignedIn = Session.IsSignedIn;
        if (_wasSignedIn) await OnSignedInAsync();
    }

    /// <summary>Called when the page opens while signed in, and again right after the user signs in on the page.</summary>
    protected virtual Task OnSignedInAsync() => Task.CompletedTask;

    private void OnSessionChanged() => _ = InvokeAsync(async () =>
    {
        var signedIn = Session.IsSignedIn;
        if (signedIn && !_wasSignedIn)
        {
            _wasSignedIn = true;
            await OnSignedInAsync();
        }
        else if (!signedIn) _wasSignedIn = false;
        StateHasChanged();
    });

    public void Dispose() => Session.Changed -= OnSessionChanged;
}
