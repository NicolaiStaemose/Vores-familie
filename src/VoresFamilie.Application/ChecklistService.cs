using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class ChecklistService(IFamilyRepository families, IChecklistRepository checklist, IClock clock)
{
    // ---- Definitions (routines and household tasks) ----

    public async Task<IReadOnlyList<ChecklistItemDto>> ListItemsAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var items = await checklist.GetItemsAsync(family.Id, ct);
        return items.Where(i => ChecklistRules.CanView(me, i)).OrderBy(i => i.Kind).ThenBy(i => i.SortOrder)
            .Select(i => ToDto(family, i)).ToList();
    }

    public async Task<ChecklistItemDto> CreateItemAsync(string userId, SaveChecklistItemRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        RequireMemberExists(family, r.MemberId);
        var all = await checklist.GetItemsAsync(family.Id, ct);
        var next = all.Where(i => SameGroup(i, r.Kind, r.Part, r.MemberId)).Select(i => i.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        var item = ChecklistItem.Create(family.Id, r.Kind, r.Part, r.MemberId, r.Title, (DaysOfWeek)r.Days, next);
        await checklist.AddItemAsync(item, ct);
        await checklist.SaveChangesAsync(ct);
        return ToDto(family, item);
    }

    public async Task<ChecklistItemDto> UpdateItemAsync(string userId, Guid itemId, SaveChecklistItemRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        var item = await GetOwnItemAsync(family, itemId, ct);
        if (item.Kind != r.Kind) throw new DomainException("Typen kan ikke ændres. Opret et nyt punkt.");
        RequireMemberExists(family, r.MemberId);
        item.Apply(r.Part, r.MemberId, r.Title, (DaysOfWeek)r.Days);
        await checklist.SaveChangesAsync(ct);
        return ToDto(family, item);
    }

    public async Task DeleteItemAsync(string userId, Guid itemId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        await checklist.RemoveItemAsync(await GetOwnItemAsync(family, itemId, ct), ct);
        await checklist.SaveChangesAsync(ct);
    }

    /// <summary>Moves a point one step up or down among the points of the same list (same child and part, or all household tasks).</summary>
    public async Task MoveItemAsync(string userId, Guid itemId, bool up, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEditor(me);
        var item = await GetOwnItemAsync(family, itemId, ct);
        var group = (await checklist.GetItemsAsync(family.Id, ct))
            .Where(i => SameGroup(i, item.Kind, item.Part, item.MemberId))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        for (var i = 0; i < group.Count; i++) group[i].MoveTo(i); // make the order explicit first
        var index = group.FindIndex(i => i.Id == item.Id);
        var swap = up ? index - 1 : index + 1;
        if (swap >= 0 && swap < group.Count)
        {
            group[index].MoveTo(swap);
            group[swap].MoveTo(index);
        }
        await checklist.SaveChangesAsync(ct);
    }

    // ---- The day ----

    /// <summary>Today's list for a person: their routines plus household tasks for them (or for everyone) that are due that weekday.</summary>
    public async Task<ChecklistDayDto> GetDayAsync(string userId, DateOnly date, Guid? memberId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var target = family.Members.FirstOrDefault(m => m.Id == (memberId ?? me.Id)) ?? throw new NotFoundException("Medlemmet findes ikke.");
        if (target.Id != me.Id && !ChecklistRules.CanSeeArea(me)) throw new ForbiddenException("Du må ikke se andres rutiner.");

        var items = (await checklist.GetItemsAsync(family.Id, ct))
            .Where(i => i.IsDueOn(date) && ChecklistRules.CanView(me, i)
                        && (i.Kind == ChecklistKind.Routine ? i.MemberId == target.Id : i.MemberId == target.Id || i.MemberId is null))
            .OrderBy(i => i.Kind).ThenBy(i => i.Part).ThenBy(i => i.SortOrder)
            .ToList();
        var checks = (await checklist.GetChecksAsync(items.Select(i => i.Id).ToList(), date, ct)).ToDictionary(c => c.ItemId);
        var canTickDate = IsTickableDate(date);

        var entries = items.Select(i =>
        {
            checks.TryGetValue(i.Id, out var check);
            return new ChecklistEntryDto(i.Id, i.Kind, i.Part, i.Title, i.MemberId, NameOf(family, i.MemberId),
                check is not null, check?.CompletedAt, NameOf(family, check?.CompletedByMemberId),
                canTickDate && ChecklistRules.CanCheck(me, i));
        }).ToList();
        return new ChecklistDayDto(date, target.Id, target.DisplayName, canTickDate, entries);
    }

    public async Task SetDoneAsync(string userId, Guid itemId, DateOnly date, bool done, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var item = await GetOwnItemAsync(family, itemId, ct);
        if (!ChecklistRules.CanView(me, item)) throw new NotFoundException("Punktet findes ikke.");
        if (!ChecklistRules.CanCheck(me, item)) throw new ForbiddenException("Du må ikke afkrydse dette punkt.");
        if (!IsTickableDate(date)) throw new DomainException("Du kan kun krydse af for i dag og i går.");
        if (!item.IsDueOn(date)) throw new DomainException("Punktet er ikke på listen den dag.");

        var existing = await checklist.FindCheckAsync(itemId, date, ct);
        if (done && existing is null)
            await checklist.AddCheckAsync(ChecklistCheck.Create(itemId, date, me.Id, clock.UtcNow), ct);
        else if (!done && existing is not null)
            await checklist.RemoveCheckAsync(existing, ct);
        await checklist.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    /// <summary>Only today and yesterday can be ticked: a late evening routine may be finished just after midnight.</summary>
    private bool IsTickableDate(DateOnly date)
    {
        var today = FamilyClock.Today(clock);
        return date == today || date == today.AddDays(-1);
    }

    /// <summary>All household tasks form one list; a routine list is one child's morning or evening.</summary>
    private static bool SameGroup(ChecklistItem i, ChecklistKind kind, RoutinePart? part, Guid? memberId) =>
        i.Kind == kind && (kind == ChecklistKind.HouseholdTask || (i.MemberId == memberId && i.Part == part));

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        return (family, family.FindMemberByUser(userId)!);
    }

    private async Task<ChecklistItem> GetOwnItemAsync(Family family, Guid itemId, CancellationToken ct)
    {
        var item = await checklist.GetItemAsync(itemId, ct);
        if (item is null || item.FamilyId != family.Id) throw new NotFoundException("Punktet findes ikke.");
        return item;
    }

    private static void RequireEditor(FamilyMember me)
    {
        if (!ChecklistRules.CanEdit(me)) throw new ForbiddenException("Du må ikke redigere rutiner og opgaver.");
    }

    private static void RequireMemberExists(Family family, Guid? memberId)
    {
        if (memberId is { } id && family.Members.All(m => m.Id != id)) throw new DomainException("Personen er ikke medlem af familien.");
    }

    private static string? NameOf(Family family, Guid? memberId) =>
        memberId is null ? null : family.Members.FirstOrDefault(m => m.Id == memberId)?.DisplayName;

    private static ChecklistItemDto ToDto(Family family, ChecklistItem i) =>
        new(i.Id, i.Kind, i.Part, i.MemberId, NameOf(family, i.MemberId), i.Title, (int)i.Days, i.SortOrder);
}

