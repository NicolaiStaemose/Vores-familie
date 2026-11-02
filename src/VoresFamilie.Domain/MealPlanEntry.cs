namespace VoresFamilie.Domain;

/// <summary>Dinner on one date. The ingredients are a copy, so changing them here never changes the saved dish.</summary>
public sealed class MealPlanEntry
{
    private MealPlanEntry() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }
    public Guid? DishId { get; private set; }
    public IReadOnlyList<IngredientLine> Ingredients { get; private set; } = [];

    public static MealPlanEntry Create(Guid familyId, DateOnly date, string title, string? notes, Guid? dishId,
        IEnumerable<IngredientLine>? ingredients)
    {
        var entry = new MealPlanEntry { Id = Guid.NewGuid(), FamilyId = familyId, Date = date };
        entry.Apply(title, notes, dishId, ingredients);
        return entry;
    }

    public void Apply(string title, string? notes, Guid? dishId, IEnumerable<IngredientLine>? ingredients)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Skriv, hvad der skal spises.");
        Title = title.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        DishId = dishId;
        Ingredients = IngredientLine.Clean(ingredients);
    }
}
