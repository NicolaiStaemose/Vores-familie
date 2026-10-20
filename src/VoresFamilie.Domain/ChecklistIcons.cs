namespace VoresFamilie.Domain;

/// <summary>Suggests an emoji for a routine step or household task from its text (Danish keywords).</summary>
public static class ChecklistIcons
{
    public const int MaxLength = 16;

    // The first match wins, so specific words come before general ones ("madpakke" before "mad", "vasketøj" before "tøj").
    private static readonly (string[] Words, string Icon)[] Rules =
    [
        (["tand", "tænder"], "🪥"), // "tænder" (teeth) does not contain "tand"
        (["madpakke"], "🥪"),
        (["drikkedunk", "drikke"], "🥤"),
        (["aftensmad", "middag", "lave mad", "lav mad", "koge", "madlavning"], "🍳"),
        (["frokost"], "🥪"),
        (["morgenmad", "spis"], "🥣"),
        (["medicin", "pille"], "💊"),
        (["toilet", "tisse"], "🚽"),
        (["vasketøj", "vaske tøj", "tøjvask", "vask tøj"], "🧺"),
        (["opvask", "tallerken", "køkken"], "🍽️"),
        (["bad", "brus"], "🚿"),
        (["vask", "hænder", "ansigt"], "🧼"),
        (["hår", "kam "], "💇"),
        (["pyjamas", "nattøj"], "🌙"),
        (["jakke", "frakke", "overtøj"], "🧥"),
        (["taske", "rygsæk", "pak "], "🎒"),
        ([" sko ", "støvler"], "👟"),
        (["tøj", "klæd"], "👕"),
        (["elektronik", "skærm", "telefon", "mobil", "ipad", "tablet", " tv "], "📱"),
        (["seng", "sove", "godnat"], "🛏️"),
        (["lektie", "læs", "bog"], "📚"),
        (["dække", "borddækning"], "🍴"),
        (["støvsug", "gulv", " fej"], "🧹"),
        (["skrald", "affald"], "🗑️"),
        (["ryd", "orden"], "🧺"),
        (["blomst", "plante"], "🌱"),
        (["hund", "gå tur", "luft"], "🐕"),
        ([" kat "], "🐈"),
        (["indkøb", "købe"], "🛒"),
    ];
    /// <summary>The suggested emoji, or null when nothing fits.</summary>
    public static string? Suggest(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var text = " " + title.Trim().ToLowerInvariant() + " ";
        foreach (var (words, icon) in Rules)
            if (words.Any(w => text.Contains(w, StringComparison.Ordinal))) return icon;
        return null;
    }

    /// <summary>Stored value: null = automatic (suggested from the text), empty = no icon, otherwise the chosen emoji.</summary>
    public static string? Normalize(string? icon)
    {
        if (icon is null) return null;
        var trimmed = icon.Trim();
        if (trimmed.Length > MaxLength) throw new DomainException("Ikonet er ugyldigt.");
        return trimmed;
    }

    /// <summary>What to show: the chosen icon, the suggestion when automatic, or nothing.</summary>
    public static string? Effective(string? stored, string title) => stored is null ? Suggest(title) : stored.Length == 0 ? null : stored;
}


