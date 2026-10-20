using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class TodoService(IFamilyRepository families, ITodoRepository todos, IClock clock)
{
    public async Task<IReadOnlyList<TodoDto>> ListAsync(string userId, TodoFilter filter, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var today = FamilyClock.Today(clock);
        var items = (await todos.GetAsync(family.Id, ct)).Where(t => TodoRules.CanView(me, t));
        items = filter switch
        {
            TodoFilter.Mine => items.Where(t => t.AssigneeMemberId == me.Id),
            TodoFilter.Open => items.Where(t => !t.IsDone),
            _ => items,
        };
        return items
            .OrderBy(t => t.IsDone)                               // open first
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue)          // soonest deadline first, no deadline last
            .ThenByDescending(t => t.CompletedAt)                 // recently finished first among the done
            .ThenBy(t => t.CreatedAt)
            .Select(t => ToDto(family, me, t, today)).ToList();
    }

    public async Task<TodoDto> CreateAsync(string userId, SaveTodoRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        RequireMemberExists(family, r.AssigneeMemberId);
        var item = TodoItem.Create(family.Id, r.Title, r.Notes, r.AssigneeMemberId, r.DueDate, me.Id, clock.UtcNow);
        await todos.AddAsync(item, ct);
        await todos.SaveChangesAsync(ct);
        return ToDto(family, me, item, FamilyClock.Today(clock));
    }

    public async Task<TodoDto> UpdateAsync(string userId, Guid id, SaveTodoRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        RequireMemberExists(family, r.AssigneeMemberId);
        var item = await GetOwnAsync(family, id, ct);
        item.Apply(r.Title, r.Notes, r.AssigneeMemberId, r.DueDate);
        await todos.SaveChangesAsync(ct);
        return ToDto(family, me, item, FamilyClock.Today(clock));
    }

    public async Task DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        await todos.RemoveAsync([await GetOwnAsync(family, id, ct)], ct);
        await todos.SaveChangesAsync(ct);
    }

    public async Task SetDoneAsync(string userId, Guid id, bool done, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var item = await GetOwnAsync(family, id, ct);
        if (!TodoRules.CanView(me, item)) throw new NotFoundException("Punktet findes ikke.");
        if (!TodoRules.CanCheck(me, item)) throw new ForbiddenException("Du må ikke afkrydse dette punkt.");
        if (done) item.Complete(me.Id, clock.UtcNow); else item.Reopen();
        await todos.SaveChangesAsync(ct);
    }

    /// <summary>Removes all finished points ("Ryd færdige").</summary>
    public async Task<int> ClearCompletedAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        var done = (await todos.GetAsync(family.Id, ct)).Where(t => t.IsDone).ToList();
        await todos.RemoveAsync(done, ct);
        await todos.SaveChangesAsync(ct);
        return done.Count;
    }

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        return (family, family.FindMemberByUser(userId)!);
    }

    private async Task<TodoItem> GetOwnAsync(Family family, Guid id, CancellationToken ct)
    {
        var item = await todos.GetByIdAsync(id, ct);
        if (item is null || item.FamilyId != family.Id) throw new NotFoundException("Punktet findes ikke.");
        return item;
    }

    private static void RequireEditor(FamilyMember me)
    {
        if (!TodoRules.CanEdit(me)) throw new ForbiddenException("Du må ikke redigere ToDo-listen.");
    }

    private static void RequireMemberExists(Family family, Guid? memberId)
    {
        if (memberId is { } id && family.Members.All(m => m.Id != id)) throw new DomainException("Personen er ikke medlem af familien.");
    }

    private static string? NameOf(Family family, Guid? memberId) =>
        memberId is null ? null : family.Members.FirstOrDefault(m => m.Id == memberId)?.DisplayName;

    private static TodoDto ToDto(Family family, FamilyMember me, TodoItem t, DateOnly today) =>
        new(t.Id, t.Title, t.Notes, t.AssigneeMemberId, NameOf(family, t.AssigneeMemberId), t.DueDate, t.IsDone, t.CompletedAt,
            NameOf(family, t.CompletedByMemberId), t.IsOverdue(today), TodoRules.CanCheck(me, t));
}
