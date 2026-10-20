using System.Text.RegularExpressions;

namespace VoresFamilie.Domain;

public sealed record HomeworkInfo(string? Subject, string? ChildName);

/// <summary>Reads the homework conventions written by the weekly "Hent lektier" task.</summary>
public static partial class HomeworkParser
{
    /// <summary>Homework is any event whose title contains "lektie" (any casing).</summary>
    public static bool IsHomework(string? summary) =>
        summary?.Contains("lektie", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Subject and child come from the "Fag:" and "Barn:" lines in the description.</summary>
    public static HomeworkInfo Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return new(null, null);
        return new(Find(SubjectLine(), description), Find(ChildLine(), description));
    }

    private static string? Find(Regex regex, string text)
    {
        var m = regex.Match(text);
        var value = m.Success ? m.Groups[1].Value.Trim() : null;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    [GeneratedRegex(@"^\s*Fag\s*:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SubjectLine();

    [GeneratedRegex(@"^\s*Barn\s*:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ChildLine();
}
