using Microsoft.EntityFrameworkCore;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

internal sealed class MealRepository(AppDbContext db) : IMealRepository
{
    public async Task<IReadOnlyList<MealPlanEntry>> GetEntriesAsync(Guid familyId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await db.MealPlanEntries.Where(e => e.FamilyId == familyId && e.Date >= from && e.Date <= to).ToListAsync(ct);

    public Task<MealPlanEntry?> FindEntryAsync(Guid familyId, DateOnly date, CancellationToken ct = default) =>
        db.MealPlanEntries.FirstOrDefaultAsync(e => e.FamilyId == familyId && e.Date == date, ct);

    public async Task AddEntryAsync(MealPlanEntry entry, CancellationToken ct = default) => await db.MealPlanEntries.AddAsync(entry, ct);

    public Task RemoveEntryAsync(MealPlanEntry entry, CancellationToken ct = default)
    {
        db.MealPlanEntries.Remove(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Dish>> GetDishesAsync(Guid familyId, CancellationToken ct = default) =>
        await db.Dishes.Where(d => d.FamilyId == familyId).ToListAsync(ct);

    public Task<Dish?> GetDishAsync(Guid id, CancellationToken ct = default) => db.Dishes.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddDishAsync(Dish dish, CancellationToken ct = default) => await db.Dishes.AddAsync(dish, ct);

    public Task RemoveDishAsync(Dish dish, CancellationToken ct = default)
    {
        db.Dishes.Remove(dish);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

internal sealed class ShoppingRepository(AppDbContext db) : IShoppingRepository
{
    public async Task<IReadOnlyList<ShoppingItem>> GetItemsAsync(Guid familyId, CancellationToken ct = default) =>
        await db.ShoppingItems.Where(i => i.FamilyId == familyId).ToListAsync(ct);

    public Task<ShoppingItem?> GetItemAsync(Guid id, CancellationToken ct = default) => db.ShoppingItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task AddItemAsync(ShoppingItem item, CancellationToken ct = default) => await db.ShoppingItems.AddAsync(item, ct);

    public Task RemoveItemsAsync(IEnumerable<ShoppingItem> items, CancellationToken ct = default)
    {
        db.ShoppingItems.RemoveRange(items);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(Guid familyId, CancellationToken ct = default) =>
        await db.FavoriteItems.Where(f => f.FamilyId == familyId).ToListAsync(ct);

    public Task<FavoriteItem?> GetFavoriteAsync(Guid id, CancellationToken ct = default) => db.FavoriteItems.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task AddFavoriteAsync(FavoriteItem favorite, CancellationToken ct = default) => await db.FavoriteItems.AddAsync(favorite, ct);

    public Task RemoveFavoriteAsync(FavoriteItem favorite, CancellationToken ct = default)
    {
        db.FavoriteItems.Remove(favorite);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PurchaseRecord>> GetPurchasesSinceAsync(Guid familyId, DateOnly since, CancellationToken ct = default) =>
        await db.PurchaseRecords.Where(p => p.FamilyId == familyId && p.BoughtOn >= since).ToListAsync(ct);

    public async Task AddPurchaseAsync(PurchaseRecord purchase, CancellationToken ct = default) => await db.PurchaseRecords.AddAsync(purchase, ct);

    public async Task RemovePurchasesOfItemAsync(Guid itemId, CancellationToken ct = default) =>
        db.PurchaseRecords.RemoveRange(await db.PurchaseRecords.Where(p => p.ItemId == itemId).ToListAsync(ct));

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

internal sealed class CleaningRepository(AppDbContext db) : ICleaningRepository
{
    public async Task<IReadOnlyList<CleaningTask>> GetAsync(Guid familyId, CancellationToken ct = default) =>
        await db.CleaningTasks.Where(t => t.FamilyId == familyId).ToListAsync(ct);

    public Task<CleaningTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.CleaningTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(CleaningTask task, CancellationToken ct = default) => await db.CleaningTasks.AddAsync(task, ct);

    public Task RemoveAsync(CleaningTask task, CancellationToken ct = default)
    {
        db.CleaningTasks.Remove(task);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
