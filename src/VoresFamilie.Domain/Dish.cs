namespace VoresFamilie.Domain;

/// <summary>A saved dish (name and ingredients) that can be put on the meal plan again.</summary>
public sealed class Dish
{
    private Dish() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public IReadOnlyList<IngredientLine> Ingredients { get; private set; } = [];

    public static Dish Create(Guid familyId, string name, IEnumerable<IngredientLine>? ingredients)
    {
        var dish = new Dish { Id = Guid.NewGuid(), FamilyId = familyId };
        dish.Apply(name, ingredients);
        return dish;
    }

    public void Apply(string name, IEnumerable<IngredientLine>? ingredients)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Retten skal have et navn.");
        Name = name.Trim();
        Ingredients = IngredientLine.Clean(ingredients);
    }
}
