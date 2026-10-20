using Microsoft.JSInterop;

namespace VoresFamilie.Web;

/// <summary>
/// Not every device has every emoji (the toothbrush is newer than many desktop emoji fonts and shows as an empty box).
/// We test once in the browser and swap the few newer emoji for an older, widely supported one when needed.
/// </summary>
public sealed class Emojis(IJSRuntime js)
{
    private static readonly Dictionary<string, string> Fallbacks = new() { ["🪥"] = "🦷" };

    private readonly HashSet<string> _missing = [];
    private bool _initialised;

    public async Task InitAsync()
    {
        if (_initialised) return;
        _initialised = true;
        foreach (var emoji in Fallbacks.Keys)
        {
            try { if (!await js.InvokeAsync<bool>("emojiSupported", emoji)) _missing.Add(emoji); }
            catch (JSException) { /* keep the original emoji if the check cannot run */ }
        }
    }

    /// <summary>The emoji to show: the original, or its fallback on devices that cannot draw it.</summary>
    public string? Show(string? emoji) =>
        emoji is not null && _missing.Contains(emoji) ? Fallbacks[emoji] : emoji;

    /// <summary>False for emoji this device cannot draw (hide them from pickers).</summary>
    public bool CanShow(string emoji) => !_missing.Contains(emoji);
}
