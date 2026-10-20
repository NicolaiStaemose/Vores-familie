using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class MealPlanService(IFamilyRepository families, IMealRepository meals, ShoppingService shopping)
{
    // ---- The plan ----

    /// <summary>Dinners from one date to another, both included.</summary>
    public async Task<IReadOnlyList<MealDto>> GetAsync(string userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from || to.DayNumber - from.DayNumber > 92) throw new DomainException("Vælg et gyldigt tidsrum på højst tre måneder.");
        var (family, me) = await LoadAsync(userId, ct);
        RequireView(me);
        return (await meals.GetEntriesAsync(family.Id, from, to, ct)).OrderBy(e => e.Date).Select(ToDto).ToList();
    }

    /// <summary>Sets what is for dinner on a date (replaces what was there).</summary>
    public async Task<MealDto> SetAsync(string userId, DateOnly date, SaveMealRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var dishId = await ValidDishAsync(family, r.DishId, ct);
        var lines = ToLines(r.Ingredients);
        var entry = await meals.FindEntryAsync(family.Id, date, ct);
        if (entry is null)
        {
            entry = MealPlanEntry.Create(family.Id, date, r.Title, r.Notes, dishId, lines);
            await meals.AddEntryAsync(entry, ct);
        }
        else entry.Apply(r.Title, r.Notes, dishId, lines);
        await meals.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task ClearAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var entry = await meals.FindEntryAsync(family.Id, date, ct);
        if (entry is null) return;
        await meals.RemoveEntryAsync(entry, ct);
        await meals.SaveChangesAsync(ct);
    }

    /// <summary>Puts the ingredients of the dinners in a period on the shopping list (skipping what is already there).</summary>
    public async Task<AddToListResultDto> AddToShoppingListAsync(string userId, AddMealsToShoppingListRequest r, CancellationToken ct = default)
    {
        if (r.To < r.From || r.To.DayNumber - r.From.DayNumber > 31) throw new DomainException("Vælg et gyldigt tidsrum på højst en måned.");
        var (family, me) = await LoadAsync(userId, ct);
        RequireView(me);
        var lines = (await meals.GetEntriesAsync(family.Id, r.From, r.To, ct)).OrderBy(e => e.Date).SelectMany(e => e.Ingredients);
        return await shopping.AddManyAsync(family, me, lines, ct);
    }

    // ---- Saved dishes ----

    public async Task<IReadOnlyList<DishDto>> ListDishesAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireView(me);
        return (await meals.GetDishesAsync(family.Id, ct)).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToDto).ToList();
    }

    public async Task<DishDto> CreateDishAsync(string userId, SaveDishRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        await EnsureNameFreeAsync(family, r.Name, null, ct);
        var dish = Dish.Create(family.Id, r.Name, ToLines(r.Ingredients));
        await meals.AddDishAsync(dish, ct);
        await meals.SaveChangesAsync(ct);
        return ToDto(dish);
    }

    public async Task<DishDto> UpdateDishAsync(string userId, Guid id, SaveDishRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var dish = await GetOwnDishAsync(family, id, ct);
        await EnsureNameFreeAsync(family, r.Name, id, ct);
        dish.Apply(r.Name, ToLines(r.Ingredients));
        await meals.SaveChangesAsync(ct);
        return ToDto(dish);
    }

    public async Task DeleteDishAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        await meals.RemoveDishAsync(await GetOwnDishAsync(family, id, ct), ct);
        await meals.SaveChangesAsync(ct);
    }

    /// <summary>Saves the dinner on a date as a dish (a dish with the same name is updated instead of duplicated).</summary>
    public async Task<DishDto> SaveAsDishAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        var entry = await meals.FindEntryAsync(family.Id, date, ct) ?? throw new NotFoundException("Der er ikke planlagt noget den dag.");
        var dish = (await meals.GetDishesAsync(family.Id, ct)).FirstOrDefault(d => SameName(d.Name, entry.Title));
        if (dish is null)
        {
            dish = Dish.Create(family.Id, entry.Title, entry.Ingredients);
            await meals.AddDishAsync(dish, ct);
        }
        else dish.Apply(dish.Name, entry.Ingredients);
        entry.Apply(entry.Title, entry.Notes, dish.Id, entry.Ingredients);
        await meals.SaveChangesAsync(ct);
        return ToDto(dish);
    }

    // ---- helpers ----

    private static bool SameName(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task EnsureNameFreeAsync(Family family, string name, Guid? exceptId, CancellationToken ct)
    {
        if ((await meals.GetDishesAsync(family.Id, ct)).Any(d => d.Id != exceptId && SameName(d.Name, name ?? "")))
            throw new DomainException("Der findes allerede en ret med det navn.");
    }

    private async Task<Guid?> ValidDishAsync(Family family, Guid? dishId, CancellationToken ct)
    {
        if (dishId is null) return null;
        var dish = await meals.GetDishAsync(dishId.Value, ct);
        return dish is not null && dish.FamilyId == family.Id ? dish.Id : null; // a dish that has since been deleted is just forgotten
    }

    private async Task<Dish> GetOwnDishAsync(Family family, Guid id, CancellationToken ct)
    {
        var dish = await meals.GetDishAsync(id, ct);
        if (dish is null || dish.FamilyId != family.Id) throw new NotFoundException("Retten findes ikke.");
        return dish;
    }

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        return (family, family.FindMemberByUser(userId)!);
    }

    private static void RequireView(FamilyMember me)
    {
        if (!MealRules.CanView(me)) throw new ForbiddenException("Du må ikke se madplanen.");
    }

    private static void RequireEdit(FamilyMember me)
    {
        if (!MealRules.CanEdit(me)) throw new ForbiddenException("Du må ikke ændre madplanen.");
    }

    private static IReadOnlyList<IngredientLine> ToLines(IReadOnlyList<IngredientDto>? dtos) =>
        (dtos ?? []).Select(d => new IngredientLine(d.Name ?? "", d.Quantity)).ToList();

    private static IReadOnlyList<IngredientDto> ToDtos(IReadOnlyList<IngredientLine> lines) =>
        lines.Select(l => new IngredientDto(l.Name, l.Quantity)).ToList();

    private static MealDto ToDto(MealPlanEntry e) => new(e.Date, e.Title, e.Notes, e.DishId, ToDtos(e.Ingredients));

    private static DishDto ToDto(Dish d) => new(d.Id, d.Name, ToDtos(d.Ingredients));
}
