namespace VoresFamilie.Domain;

/// <summary>One line in a recipe or on a shopping list, for example "tomater" and "400 g".</summary>
public sealed record IngredientLine(string Name, string? Quantity)
{
    public const int MaxLines = 60;

    /// <summary>Trims the lines, drops empty ones and limits the count.</summary>
    public static IReadOnlyList<IngredientLine> Clean(IEnumerable<IngredientLine>? lines) =>
        (lines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Name))
            .Select(l => new IngredientLine(l.Name.Trim(), string.IsNullOrWhiteSpace(l.Quantity) ? null : l.Quantity.Trim()))
            .Take(MaxLines)
            .ToList();
}
