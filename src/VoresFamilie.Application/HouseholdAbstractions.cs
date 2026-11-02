using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public interface IMealRepository
{
    Task<IReadOnlyList<MealPlanEntry>> GetEntriesAsync(Guid familyId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<MealPlanEntry?> FindEntryAsync(Guid familyId, DateOnly date, CancellationToken ct = default);
    Task AddEntryAsync(MealPlanEntry entry, CancellationToken ct = default);
    Task RemoveEntryAsync(MealPlanEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<Dish>> GetDishesAsync(Guid familyId, CancellationToken ct = default);
    Task<Dish?> GetDishAsync(Guid id, CancellationToken ct = default);
    Task AddDishAsync(Dish dish, CancellationToken ct = default);
    Task RemoveDishAsync(Dish dish, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IShoppingRepository
{
    Task<IReadOnlyList<ShoppingItem>> GetItemsAsync(Guid familyId, CancellationToken ct = default);
    Task<ShoppingItem?> GetItemAsync(Guid id, CancellationToken ct = default);
    Task AddItemAsync(ShoppingItem item, CancellationToken ct = default);
    Task RemoveItemsAsync(IEnumerable<ShoppingItem> items, CancellationToken ct = default);

    Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(Guid familyId, CancellationToken ct = default);
    Task<FavoriteItem?> GetFavoriteAsync(Guid id, CancellationToken ct = default);
    Task AddFavoriteAsync(FavoriteItem favorite, CancellationToken ct = default);
    Task RemoveFavoriteAsync(FavoriteItem favorite, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseRecord>> GetPurchasesSinceAsync(Guid familyId, DateOnly since, CancellationToken ct = default);
    Task AddPurchaseAsync(PurchaseRecord purchase, CancellationToken ct = default);
    Task RemovePurchasesOfItemAsync(Guid itemId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICleaningRepository
{
    Task<IReadOnlyList<CleaningTask>> GetAsync(Guid familyId, CancellationToken ct = default);
    Task<CleaningTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(CleaningTask task, CancellationToken ct = default);
    Task RemoveAsync(CleaningTask task, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
