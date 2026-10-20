using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public interface IChecklistRepository
{
    Task<IReadOnlyList<ChecklistItem>> GetItemsAsync(Guid familyId, CancellationToken ct = default);
    Task<ChecklistItem?> GetItemAsync(Guid itemId, CancellationToken ct = default);
    Task AddItemAsync(ChecklistItem item, CancellationToken ct = default);
    /// <summary>Removes the item together with all its ticks.</summary>
    Task RemoveItemAsync(ChecklistItem item, CancellationToken ct = default);

    Task<IReadOnlyList<ChecklistCheck>> GetChecksAsync(IReadOnlyCollection<Guid> itemIds, DateOnly date, CancellationToken ct = default);
    Task<ChecklistCheck?> FindCheckAsync(Guid itemId, DateOnly date, CancellationToken ct = default);
    Task AddCheckAsync(ChecklistCheck check, CancellationToken ct = default);
    Task RemoveCheckAsync(ChecklistCheck check, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> GetAsync(Guid familyId, CancellationToken ct = default);
    Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(TodoItem item, CancellationToken ct = default);
    Task RemoveAsync(IEnumerable<TodoItem> items, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>"Today" for the family: the date in Copenhagen, not UTC.</summary>
public static class FamilyClock
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static DateOnly Today(IClock clock) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, Zone).DateTime);
}
