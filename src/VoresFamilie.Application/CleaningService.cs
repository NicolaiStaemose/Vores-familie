using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed class CleaningService(IFamilyRepository families, ICleaningRepository cleaning, IClock clock)
{
    public async Task<IReadOnlyList<CleaningDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var today = FamilyClock.Today(clock);
        return (await cleaning.GetAsync(family.Id, ct))
            .Where(t => CleaningRules.CanView(me, t))
            .OrderBy(t => t.Room, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.NextDue).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => ToDto(family, me, t, today)).ToList();
    }

    public async Task<CleaningDto> CreateAsync(string userId, SaveCleaningRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        RequireMemberExists(family, r.AssigneeMemberId);
        var today = FamilyClock.Today(clock);
        var task = CleaningTask.Create(family.Id, r.Name, r.Room, r.IntervalDays, r.AssigneeMemberId, today);
        await cleaning.AddAsync(task, ct);
        await cleaning.SaveChangesAsync(ct);
        return ToDto(family, me, task, today);
    }

    public async Task<CleaningDto> UpdateAsync(string userId, Guid id, SaveCleaningRequest r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        RequireMemberExists(family, r.AssigneeMemberId);
        var task = await GetOwnAsync(family, id, ct);
        task.Apply(r.Name, r.Room, r.IntervalDays, r.AssigneeMemberId);
        await cleaning.SaveChangesAsync(ct);
        return ToDto(family, me, task, FamilyClock.Today(clock));
    }

    public async Task DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        RequireEdit(me);
        await cleaning.RemoveAsync(await GetOwnAsync(family, id, ct), ct);
        await cleaning.SaveChangesAsync(ct);
    }

    public async Task<CleaningDto> MarkDoneAsync(string userId, Guid id, MarkCleaningDoneRequest? r, CancellationToken ct = default)
    {
        var (family, me) = await LoadAsync(userId, ct);
        var task = await GetOwnAsync(family, id, ct);
        if (!CleaningRules.CanView(me, task)) throw new NotFoundException("Opgaven findes ikke.");
        if (!CleaningRules.CanMarkDone(me, task)) throw new ForbiddenException("Du må ikke markere denne opgave som gjort.");
        var today = FamilyClock.Today(clock);
        task.MarkDone(r?.Date ?? today, me.Id, today);
        await cleaning.SaveChangesAsync(ct);
        return ToDto(family, me, task, today);
    }

    private async Task<(Family, FamilyMember)> LoadAsync(string userId, CancellationToken ct)
    {
        var family = await families.GetByUserAsync(userId, ct) ?? throw new NotFoundException("Du er ikke medlem af en familie.");
        return (family, family.FindMemberByUser(userId)!);
    }

    private async Task<CleaningTask> GetOwnAsync(Family family, Guid id, CancellationToken ct)
    {
        var task = await cleaning.GetByIdAsync(id, ct);
        if (task is null || task.FamilyId != family.Id) throw new NotFoundException("Opgaven findes ikke.");
        return task;
    }

    private static void RequireEdit(FamilyMember me)
    {
        if (!CleaningRules.CanEdit(me)) throw new ForbiddenException("Du må ikke redigere rengøringsplanen.");
    }

    private static void RequireMemberExists(Family family, Guid? memberId)
    {
        if (memberId is { } id && family.Members.All(m => m.Id != id)) throw new DomainException("Personen er ikke medlem af familien.");
    }

    private static string? NameOf(Family family, Guid? memberId) =>
        memberId is null ? null : family.Members.FirstOrDefault(m => m.Id == memberId)?.DisplayName;

    private static CleaningDto ToDto(Family family, FamilyMember me, CleaningTask t, DateOnly today) =>
        new(t.Id, t.Name, t.Room, t.IntervalDays, t.AssigneeMemberId, NameOf(family, t.AssigneeMemberId), t.LastDoneOn,
            NameOf(family, t.LastDoneByMemberId), t.NextDue, t.DaysOverdue(today), t.StatusOn(today), CleaningRules.CanMarkDone(me, t));
}
