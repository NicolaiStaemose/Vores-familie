using Microsoft.EntityFrameworkCore;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

internal sealed class ChecklistRepository(AppDbContext db) : IChecklistRepository
{
    public async Task<IReadOnlyList<ChecklistItem>> GetItemsAsync(Guid familyId, CancellationToken ct = default) =>
        await db.ChecklistItems.Where(i => i.FamilyId == familyId).ToListAsync(ct);

    public Task<ChecklistItem?> GetItemAsync(Guid itemId, CancellationToken ct = default) =>
        db.ChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public async Task AddItemAsync(ChecklistItem item, CancellationToken ct = default) =>
        await db.ChecklistItems.AddAsync(item, ct);

    public async Task RemoveItemAsync(ChecklistItem item, CancellationToken ct = default)
    {
        db.ChecklistChecks.RemoveRange(await db.ChecklistChecks.Where(c => c.ItemId == item.Id).ToListAsync(ct));
        db.ChecklistItems.Remove(item);
    }

    public async Task<IReadOnlyList<ChecklistCheck>> GetChecksAsync(IReadOnlyCollection<Guid> itemIds, DateOnly date, CancellationToken ct = default)
    {
        var ids = itemIds.ToList();
        return await db.ChecklistChecks.Where(c => c.Date == date && ids.Contains(c.ItemId)).ToListAsync(ct);
    }

    public Task<ChecklistCheck?> FindCheckAsync(Guid itemId, DateOnly date, CancellationToken ct = default) =>
        db.ChecklistChecks.FirstOrDefaultAsync(c => c.ItemId == itemId && c.Date == date, ct);

    public async Task AddCheckAsync(ChecklistCheck check, CancellationToken ct = default) =>
        await db.ChecklistChecks.AddAsync(check, ct);

    public Task RemoveCheckAsync(ChecklistCheck check, CancellationToken ct = default)
    {
        db.ChecklistChecks.Remove(check);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

internal sealed class TodoRepository(AppDbContext db) : ITodoRepository
{
    public async Task<IReadOnlyList<TodoItem>> GetAsync(Guid familyId, CancellationToken ct = default) =>
        await db.TodoItems.Where(t => t.FamilyId == familyId).ToListAsync(ct);

    public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TodoItems.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(TodoItem item, CancellationToken ct = default) => await db.TodoItems.AddAsync(item, ct);

    public Task RemoveAsync(IEnumerable<TodoItem> items, CancellationToken ct = default)
    {
        db.TodoItems.RemoveRange(items);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
