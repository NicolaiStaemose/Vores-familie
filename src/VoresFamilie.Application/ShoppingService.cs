using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class ShoppingService(IFamilyRepository families, IShoppingRepository shopping, IClock clock)
{
    // ---- The list ----

    public async Task<IReadOnlyList<ShoppingItemDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireView(me);
        return (await shopping.GetItemsAsync(family.Id, ct))
            .OrderBy(i => i.IsBought).ThenBy(i => i.Category).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(i => ToDto(family, i)).ToList();
    }

    public async Task<ShoppingItemDto> AddAsync(string userId, SaveShoppingItemRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var items = await shopping.GetItemsAsync(family.Id, ct);
        if (items.Any(i => !i.IsBought && i.HasName(r.Name))) throw new DomainException("Den står allerede på listen.");
        var item = ShoppingItem.Create(family.Id, r.Name, r.Quantity, Category(r), me.Id, clock.UtcNow);
        await shopping.AddItemAsync(item, ct);
        await shopping.SaveChangesAsync(ct);
        return ToDto(family, item);
    }

    public async Task<ShoppingItemDto> UpdateAsync(string userId, Guid id, SaveShoppingItemRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var item = await GetOwnAsync(family, id, ct);
        item.Apply(r.Name, r.Quantity, Category(r));
        await shopping.SaveChangesAsync(ct);
        return ToDto(family, item);
    }

    public async Task DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        await shopping.RemoveItemsAsync([await GetOwnAsync(family, id, ct)], ct);
        await shopping.SaveChangesAsync(ct);
    }

    public async Task SetBoughtAsync(string userId, Guid id, bool bought, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var item = await GetOwnAsync(family, id, ct);
        if (bought && !item.IsBought)
        {
            item.Buy(me.Id, clock.UtcNow);
            await shopping.AddPurchaseAsync(PurchaseRecord.For(item, FamilyClock.Today(clock)), ct);
        }
        else if (!bought && item.IsBought)
        {
            item.Unbuy();
            await shopping.RemovePurchasesOfItemAsync(item.Id, ct); // the tick was a mistake, so it should not count as a purchase
        }
        await shopping.SaveChangesAsync(ct);
    }

    /// <summary>Items bought about weekly that are not on the list now.</summary>
    public async Task<IReadOnlyList<HabitDto>> ListHabitsAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireView(me);
        var today = FamilyClock.Today(clock);
        var purchases = await shopping.GetPurchasesSinceAsync(family.Id, today.AddDays(-ShoppingHabits.WindowDays), ct);
        // An item that is ticked but not cleared yet still counts as "on the list", so it is not suggested a second time.
        var onList = (await shopping.GetItemsAsync(family.Id, ct)).Select(i => i.Name);
        return ShoppingHabits.Suggest(purchases, onList, today)
            .Select(h => new HabitDto(h.Name, h.Quantity, h.Category, h.TimesBought, h.AverageDays, h.DaysSinceLast)).ToList();
    }

    /// <summary>Removes everything that has been bought. Returns how many.</summary>
    public async Task<int> ClearBoughtAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var bought = (await shopping.GetItemsAsync(family.Id, ct)).Where(i => i.IsBought).ToList();
        await shopping.RemoveItemsAsync(bought, ct);
        await shopping.SaveChangesAsync(ct);
        return bought.Count;
    }

    public static CategorySuggestionDto SuggestCategory(string? name) => new(ShoppingCategories.Suggest(name));

    // ---- Regular items ----

    public async Task<IReadOnlyList<FavoriteDto>> ListFavoritesAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireView(me);
        return (await shopping.GetFavoritesAsync(family.Id, ct))
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToDto).ToList();
    }

    public async Task<FavoriteDto> AddFavoriteAsync(string userId, SaveShoppingItemRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var existing = await shopping.GetFavoritesAsync(family.Id, ct);
        if (existing.Any(f => string.Equals(f.Name, r.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("Den findes allerede blandt de faste varer.");
        var favorite = FavoriteItem.Create(family.Id, r.Name ?? "", r.Quantity, Category(r));
        await shopping.AddFavoriteAsync(favorite, ct);
        await shopping.SaveChangesAsync(ct);
        return ToDto(favorite);
    }

    public async Task DeleteFavoriteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var favorite = await shopping.GetFavoriteAsync(id, ct);
        if (favorite is null || favorite.FamilyId != family.Id) throw new NotFoundException("Den faste vare findes ikke.");
        await shopping.RemoveFavoriteAsync(favorite, ct);
        await shopping.SaveChangesAsync(ct);
    }

    /// <summary>Puts a regular item on the list. Returns false when it was already there.</summary>
    public async Task<bool> AddFavoriteToListAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var favorite = await shopping.GetFavoriteAsync(id, ct);
        if (favorite is null || favorite.FamilyId != family.Id) throw new NotFoundException("Den faste vare findes ikke.");
        var result = await AddManyAsync(family, me, [new IngredientLine(favorite.Name, favorite.Quantity)], ct);
        return result.Added == 1;
    }

    // ---- Used by the meal plan ----

    /// <summary>Adds lines to the list, skipping items that are already on it (not bought, same name, any casing).</summary>
    internal async Task<AddToListResultDto> AddManyAsync(Family family, FamilyMember me, IEnumerable<IngredientLine> lines, CancellationToken ct)
    {
        RequireEdit(me);
        var open = (await shopping.GetItemsAsync(family.Id, ct)).Where(i => !i.IsBought).ToList();
        var names = new HashSet<string>(open.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
        int added = 0, skipped = 0;
        foreach (var line in lines)
        {
            if (!names.Add(line.Name.Trim())) { skipped++; continue; }
            await shopping.AddItemAsync(ShoppingItem.Create(family.Id, line.Name, line.Quantity, null, me.Id, clock.UtcNow), ct);
            added++;
        }
        await shopping.SaveChangesAsync(ct);
        return new AddToListResultDto(added, skipped);
    }

    // ---- helpers ----

    private static ShoppingCategory? Category(SaveShoppingItemRequest r) => r.AutoCategory ? null : r.Category;

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        return (family, family.FindMemberByUser(userId)!);
    }

    private async Task<ShoppingItem> GetOwnAsync(Family family, Guid id, CancellationToken ct)
    {
        var item = await shopping.GetItemAsync(id, ct);
        if (item is null || item.FamilyId != family.Id) throw new NotFoundException("Varen findes ikke.");
        return item;
    }

    private static void RequireView(FamilyMember me)
    {
        if (!ShoppingRules.CanView(me)) throw new ForbiddenException("Du må ikke se indkøbslisten.");
    }

    private static void RequireEdit(FamilyMember me)
    {
        if (!ShoppingRules.CanEdit(me)) throw new ForbiddenException("Du må ikke ændre indkøbslisten.");
    }

    private static string? NameOf(Family family, Guid? memberId) =>
        memberId is null ? null : family.Members.FirstOrDefault(m => m.Id == memberId)?.DisplayName;

    private static ShoppingItemDto ToDto(Family family, ShoppingItem i) =>
        new(i.Id, i.Name, i.Quantity, i.Category, i.IsBought, i.BoughtAt, NameOf(family, i.BoughtByMemberId), NameOf(family, i.CreatedByMemberId));

    private static FavoriteDto ToDto(FavoriteItem f) => new(f.Id, f.Name, f.Quantity, f.Category);
}
