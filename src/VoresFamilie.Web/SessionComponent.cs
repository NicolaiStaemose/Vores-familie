using Microsoft.AspNetCore.Components;

namespace VoresFamilie.Web;

/// <summary>Page base class that re-renders when the signed-in user or family changes.</summary>
public abstract class SessionComponent : ComponentBase, IDisposable
{
    [Inject] protected Session Session { get; set; } = default!;

    protected override void OnInitialized() => Session.Changed += OnSessionChanged;

    private void OnSessionChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Session.Changed -= OnSessionChanged;
}
