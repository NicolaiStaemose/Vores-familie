using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Application.Tests;

internal sealed class MemChecklist : IChecklistRepository
{
    public List<ChecklistItem> Items { get; } = [];
    public List<ChecklistCheck> Checks { get; } = [];
    public Task<IReadOnlyList<ChecklistItem>> GetItemsAsync(Guid familyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChecklistItem>>(Items.Where(i => i.FamilyId == familyId).ToList());
    public Task<ChecklistItem?> GetItemAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task AddItemAsync(ChecklistItem item, CancellationToken ct = default) { Items.Add(item); return Task.CompletedTask; }
    public Task RemoveItemAsync(ChecklistItem item, CancellationToken ct = default)
    {
        Items.Remove(item);
        Checks.RemoveAll(c => c.ItemId == item.Id);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<ChecklistCheck>> GetChecksAsync(IReadOnlyCollection<Guid> ids, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChecklistCheck>>(Checks.Where(c => ids.Contains(c.ItemId) && c.Date == date).ToList());
    public Task<ChecklistCheck?> FindCheckAsync(Guid itemId, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult(Checks.FirstOrDefault(c => c.ItemId == itemId && c.Date == date));
    public Task AddCheckAsync(ChecklistCheck c, CancellationToken ct = default) { Checks.Add(c); return Task.CompletedTask; }
    public Task RemoveCheckAsync(ChecklistCheck c, CancellationToken ct = default) { Checks.Remove(c); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class MemTodos : ITodoRepository
{
    public List<TodoItem> Items { get; } = [];
    public Task<IReadOnlyList<TodoItem>> GetAsync(Guid familyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(Items.Where(i => i.FamilyId == familyId).ToList());
    public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task AddAsync(TodoItem item, CancellationToken ct = default) { Items.Add(item); return Task.CompletedTask; }
    public Task RemoveAsync(IEnumerable<TodoItem> items, CancellationToken ct = default) { foreach (var i in items.ToList()) Items.Remove(i); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Family with an admin and two children, wired to in-memory repositories. Time starts on Monday 19 Oct 2026, 14:00 Copenhagen.</summary>
internal sealed class DailyEnv
{
    public Env Base { get; } = new();
    public MemChecklist Checklist { get; } = new();
    public MemTodos Todos { get; } = new();
    public ChecklistService Service { get; }
    public TodoService TodoService { get; }

    public const string Admin = Env.AdminUser, Child = Env.ChildUser, Sibling = Env.SiblingUser;
    public Guid ChildId => Base.Child.Id;
    public Guid SiblingId => Base.Sibling.Id;
    public static readonly DateOnly Monday = new(2026, 10, 19);

    public DailyEnv()
    {
        Service = new ChecklistService(Base.Families, Checklist, Base.Time);
        TodoService = new TodoService(Base.Families, Todos, Base.Time);
    }

    public void GoToDay(DateOnly d) => Base.Time.UtcNow = new DateTimeOffset(d.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    public Task<ChecklistItemDto> AddRoutine(string user, Guid childId, RoutinePart part, string title, DaysOfWeek days = DaysOfWeek.All) =>
        Service.CreateItemAsync(user, new SaveChecklistItemRequest(ChecklistKind.Routine, part, childId, title, (int)days));

    public Task<ChecklistItemDto> AddTask(string user, string title, Guid? who, DaysOfWeek days = DaysOfWeek.All) =>
        Service.CreateItemAsync(user, new SaveChecklistItemRequest(ChecklistKind.HouseholdTask, null, who, title, (int)days));
}

public class ChecklistServiceTests
{
    [Fact]
    public async Task Day_shows_own_routines_and_tasks_for_me_or_everyone_on_the_right_weekday()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "Børst tænder");
        await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Evening, "Pak skoletaske");
        await env.AddRoutine(DailyEnv.Admin, env.SiblingId, RoutinePart.Morning, "Noahs rutine");
        await env.AddTask(DailyEnv.Admin, "Smøre madpakker", env.ChildId, DaysOfWeek.Weekdays);
        await env.AddTask(DailyEnv.Admin, "Rydde køkken", null);
        await env.AddTask(DailyEnv.Admin, "Lave aftensmad", env.SiblingId);
        await env.AddTask(DailyEnv.Admin, "Støvsuge", env.ChildId, DaysOfWeek.Saturday);

        var day = await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday, null);
        Assert.Equal("Emma", day.MemberName);
        Assert.Equal(["Børst tænder", "Pak skoletaske", "Smøre madpakker", "Rydde køkken"], day.Entries.Select(e => e.Title));
        Assert.Equal([RoutinePart.Morning, RoutinePart.Evening], day.Entries.Where(e => e.Part is not null).Select(e => e.Part!.Value));

        var saturday = await env.Service.GetDayAsync(DailyEnv.Child, new DateOnly(2026, 10, 24), null);
        Assert.Equal(["Børst tænder", "Pak skoletaske", "Rydde køkken", "Støvsuge"], saturday.Entries.Select(e => e.Title));
    }

    [Fact]
    public async Task Child_ticks_own_items_and_a_new_day_starts_clean()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        var item = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "Børst tænder");

        await env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday, true);
        await env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday, true); // idempotent
        var done = (await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday, null)).Entries.Single();
        Assert.True(done.Done);
        Assert.Equal("Emma", done.CompletedByName);
        Assert.Single(env.Checklist.Checks);

        env.GoToDay(DailyEnv.Monday.AddDays(1));
        var tuesday = (await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday.AddDays(1), null)).Entries.Single();
        Assert.False(tuesday.Done);

        env.GoToDay(DailyEnv.Monday);
        await env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday, false);
        Assert.Empty(env.Checklist.Checks);
    }

    [Fact]
    public async Task Only_today_and_yesterday_can_be_ticked()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        var item = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Evening, "Pak taske");
        await env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday.AddDays(-1), true); // yesterday: fine
        await Assert.ThrowsAsync<DomainException>(() => env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday.AddDays(-2), true));
        await Assert.ThrowsAsync<DomainException>(() => env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday.AddDays(1), true));

        var old = await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday.AddDays(-3), null);
        Assert.False(old.CanTickDate);
        Assert.All(old.Entries, e => Assert.False(e.CanCheck));
        Assert.True((await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday, null)).CanTickDate);
    }

    [Fact]
    public async Task Today_follows_Copenhagen_time_not_UTC()
    {
        var env = new DailyEnv();
        env.Base.Time.UtcNow = new DateTimeOffset(2026, 10, 19, 23, 30, 0, TimeSpan.Zero); // already Tuesday 01:30 in Copenhagen
        var item = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Evening, "x");
        await env.Service.SetDoneAsync(DailyEnv.Child, item.Id, new DateOnly(2026, 10, 20), true);
        await Assert.ThrowsAsync<DomainException>(() => env.Service.SetDoneAsync(DailyEnv.Child, item.Id, new DateOnly(2026, 10, 21), true));
    }

    [Fact]
    public async Task Sibling_cannot_tick_anothers_item_but_admin_can()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        var item = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "x");
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Service.SetDoneAsync(DailyEnv.Sibling, item.Id, DailyEnv.Monday, true));
        await env.Service.SetDoneAsync(DailyEnv.Admin, item.Id, DailyEnv.Monday, true);
        var viewedByAdmin = await env.Service.GetDayAsync(DailyEnv.Admin, DailyEnv.Monday, env.ChildId);
        Assert.Equal("Admin", viewedByAdmin.Entries.Single().CompletedByName);
    }

    [Fact]
    public async Task Item_not_due_that_weekday_cannot_be_ticked()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        var item = await env.AddTask(DailyEnv.Admin, "Støvsuge", env.ChildId, DaysOfWeek.Saturday);
        await Assert.ThrowsAsync<DomainException>(() => env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday, true));
    }

    [Fact]
    public async Task Task_without_owner_can_be_ticked_by_a_child()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        var item = await env.AddTask(DailyEnv.Admin, "Rydde køkken", null);
        await env.Service.SetDoneAsync(DailyEnv.Sibling, item.Id, DailyEnv.Monday, true);
        var day = await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday, null);
        Assert.True(day.Entries.Single().Done);
        Assert.Equal("Noah", day.Entries.Single().CompletedByName);
    }

    [Fact]
    public async Task Children_cannot_edit_but_can_see_others_when_allowed()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.AddRoutine(DailyEnv.Child, env.ChildId, RoutinePart.Morning, "x"));
        await env.AddRoutine(DailyEnv.Admin, env.SiblingId, RoutinePart.Morning, "Noahs");
        Assert.Single((await env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday, env.SiblingId)).Entries);

        env.Base.Family.SetPermission(env.ChildId, Area.Routines, AccessLevel.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Service.GetDayAsync(DailyEnv.Child, DailyEnv.Monday, env.SiblingId));
    }

    [Fact]
    public async Task Update_and_delete_items_and_their_ticks()
    {
        var env = new DailyEnv();
        env.GoToDay(DailyEnv.Monday);
        var item = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "Gammel");
        await env.Service.SetDoneAsync(DailyEnv.Child, item.Id, DailyEnv.Monday, true);

        var changed = await env.Service.UpdateItemAsync(DailyEnv.Admin, item.Id,
            new SaveChecklistItemRequest(ChecklistKind.Routine, RoutinePart.Evening, env.ChildId, "Ny", (int)DaysOfWeek.Weekdays));
        Assert.Equal("Ny", changed.Title);
        Assert.Equal(RoutinePart.Evening, changed.Part);
        await Assert.ThrowsAsync<DomainException>(() => env.Service.UpdateItemAsync(DailyEnv.Admin, item.Id,
            new SaveChecklistItemRequest(ChecklistKind.HouseholdTask, null, null, "x", 127)));

        await env.Service.DeleteItemAsync(DailyEnv.Admin, item.Id);
        Assert.Empty(env.Checklist.Items);
        Assert.Empty(env.Checklist.Checks);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Service.DeleteItemAsync(DailyEnv.Child, item.Id));
    }

    [Fact]
    public async Task Items_can_be_moved_up_and_down_within_their_list()
    {
        var env = new DailyEnv();
        var a = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "A");
        var b = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "B");
        var c = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Morning, "C");
        var evening = await env.AddRoutine(DailyEnv.Admin, env.ChildId, RoutinePart.Evening, "Aften");

        async Task<string> OrderAsync() => string.Join("", (await env.Service.ListItemsAsync(DailyEnv.Admin))
            .Where(i => i.Part == RoutinePart.Morning).OrderBy(i => i.SortOrder).Select(i => i.Title));

        Assert.Equal("ABC", await OrderAsync());
        await env.Service.MoveItemAsync(DailyEnv.Admin, c.Id, up: true);
        Assert.Equal("ACB", await OrderAsync());
        await env.Service.MoveItemAsync(DailyEnv.Admin, a.Id, up: true); // already first: no change
        Assert.Equal("ACB", await OrderAsync());
        await env.Service.MoveItemAsync(DailyEnv.Admin, a.Id, up: false);
        Assert.Equal("CAB", await OrderAsync());
        Assert.Equal(0, env.Checklist.Items.Single(i => i.Id == evening.Id).SortOrder); // the evening list is untouched
    }

    [Fact]
    public async Task Cannot_assign_to_someone_outside_the_family()
    {
        var env = new DailyEnv();
        await Assert.ThrowsAsync<DomainException>(() => env.AddTask(DailyEnv.Admin, "x", Guid.NewGuid()));
    }
}

public class TodoServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Fact]
    public async Task Admin_creates_and_assignee_child_ticks()
    {
        var env = new DailyEnv();
        env.GoToDay(Today);
        var todo = await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Tømme opvasker", "Før middag", env.ChildId, Today.AddDays(2)));
        Assert.Equal("Emma", todo.AssigneeName);
        Assert.True((await env.TodoService.ListAsync(DailyEnv.Child, TodoFilter.Mine)).Single().CanCheck);

        await env.TodoService.SetDoneAsync(DailyEnv.Child, todo.Id, true);
        var done = (await env.TodoService.ListAsync(DailyEnv.Admin, TodoFilter.All)).Single();
        Assert.True(done.IsDone);
        Assert.Equal("Emma", done.CompletedByName);
        Assert.Empty(await env.TodoService.ListAsync(DailyEnv.Admin, TodoFilter.Open));

        await env.TodoService.SetDoneAsync(DailyEnv.Child, todo.Id, false);
        Assert.False((await env.TodoService.ListAsync(DailyEnv.Admin, TodoFilter.All)).Single().IsDone);
    }

    [Fact]
    public async Task Child_cannot_create_edit_or_tick_others_items()
    {
        var env = new DailyEnv();
        var req = new SaveTodoRequest("x", null, null, null);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.TodoService.CreateAsync(DailyEnv.Child, req));
        var todo = await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("For Noah", null, env.SiblingId, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.TodoService.SetDoneAsync(DailyEnv.Child, todo.Id, true));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.TodoService.UpdateAsync(DailyEnv.Child, todo.Id, req));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.TodoService.DeleteAsync(DailyEnv.Child, todo.Id));
    }

    [Fact]
    public async Task List_is_sorted_with_deadlines_first_and_done_last_and_marks_overdue()
    {
        var env = new DailyEnv();
        env.GoToDay(Today);
        await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Ingen frist", null, null, null));
        await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Sen", null, null, Today.AddDays(5)));
        await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Forfalden", null, null, Today.AddDays(-1)));
        var finished = await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Færdig", null, null, Today.AddDays(-9)));
        await env.TodoService.SetDoneAsync(DailyEnv.Admin, finished.Id, true);

        var list = await env.TodoService.ListAsync(DailyEnv.Admin, TodoFilter.All);
        Assert.Equal(["Forfalden", "Sen", "Ingen frist", "Færdig"], list.Select(t => t.Title));
        Assert.Equal(["Forfalden"], list.Where(t => t.IsOverdue).Select(t => t.Title)); // a finished item is not overdue
    }

    [Fact]
    public async Task Clear_completed_removes_only_finished_items()
    {
        var env = new DailyEnv();
        var a = await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("A", null, null, null));
        await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("B", null, null, null));
        await env.TodoService.SetDoneAsync(DailyEnv.Admin, a.Id, true);

        Assert.Equal(1, await env.TodoService.ClearCompletedAsync(DailyEnv.Admin));
        Assert.Equal(["B"], (await env.TodoService.ListAsync(DailyEnv.Admin, TodoFilter.All)).Select(t => t.Title));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.TodoService.ClearCompletedAsync(DailyEnv.Child));
    }

    [Fact]
    public async Task Child_without_todo_access_sees_only_own_items()
    {
        var env = new DailyEnv();
        await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Fælles", null, null, null));
        await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Til Emma", null, env.ChildId, null));
        env.Base.Family.SetPermission(env.ChildId, Area.Todo, AccessLevel.None);
        Assert.Equal(["Til Emma"], (await env.TodoService.ListAsync(DailyEnv.Child, TodoFilter.All)).Select(t => t.Title));
    }

    [Fact]
    public async Task Update_and_delete()
    {
        var env = new DailyEnv();
        var todo = await env.TodoService.CreateAsync(DailyEnv.Admin, new SaveTodoRequest("Gammel", null, null, null));
        var changed = await env.TodoService.UpdateAsync(DailyEnv.Admin, todo.Id, new SaveTodoRequest("Ny", "noter", env.ChildId, Today));
        Assert.Equal(("Ny", "noter", "Emma"), (changed.Title, changed.Notes, changed.AssigneeName));
        await env.TodoService.DeleteAsync(DailyEnv.Admin, todo.Id);
        Assert.Empty(env.Todos.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => env.TodoService.DeleteAsync(DailyEnv.Admin, todo.Id));
    }
}
