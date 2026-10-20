namespace VoresFamilie.Domain;

public sealed class ShoppingItem
{
    private ShoppingItem() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>Free text such as "2 stk." or "500 g".</summary>
    public string? Quantity { get; private set; }
    public ShoppingCategory Category { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public DateTimeOffset? BoughtAt { get; private set; }
    public Guid? BoughtByMemberId { get; private set; }

    public bool IsBought => BoughtAt is not null;

    public static ShoppingItem Create(Guid familyId, string name, string? quantity, ShoppingCategory? category,
        Guid createdBy, DateTimeOffset now)
    {
        var item = new ShoppingItem { Id = Guid.NewGuid(), FamilyId = familyId, CreatedByMemberId = createdBy, CreatedAt = now };
        item.Apply(name, quantity, category);
        return item;
    }

    /// <summary>A null category means "work it out from the name".</summary>
    public void Apply(string name, string? quantity, ShoppingCategory? category)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Skriv, hvad der skal købes.");
        Name = name.Trim();
        Quantity = string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim();
        Category = category ?? ShoppingCategories.Suggest(Name);
    }

    public void Buy(Guid memberId, DateTimeOffset now)
    {
        if (IsBought) return;
        BoughtAt = now;
        BoughtByMemberId = memberId;
    }

    public void Unbuy()
    {
        BoughtAt = null;
        BoughtByMemberId = null;
    }

    public bool HasName(string name) => string.Equals(Name, name.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>A regular item ("faste varer") that can be put on the list with one tap.</summary>
public sealed class FavoriteItem
{
    private FavoriteItem() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Quantity { get; private set; }
    public ShoppingCategory Category { get; private set; }

    public static FavoriteItem Create(Guid familyId, string name, string? quantity, ShoppingCategory? category)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Skriv, hvad den faste vare hedder.");
        var trimmed = name.Trim();
        return new FavoriteItem
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            Name = trimmed,
            Quantity = string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim(),
            Category = category ?? ShoppingCategories.Suggest(trimmed),
        };
    }
}
