using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Application.Tests;

internal sealed class MemMeals : IMealRepository
{
    public List<MealPlanEntry> Entries { get; } = [];
    public List<Dish> Dishes { get; } = [];
    public Task<IReadOnlyList<MealPlanEntry>> GetEntriesAsync(Guid familyId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MealPlanEntry>>(Entries.Where(e => e.FamilyId == familyId && e.Date >= from && e.Date <= to).ToList());
    public Task<MealPlanEntry?> FindEntryAsync(Guid familyId, DateOnly date, CancellationToken ct = default) =>
        Task.FromResult(Entries.FirstOrDefault(e => e.FamilyId == familyId && e.Date == date));
    public Task AddEntryAsync(MealPlanEntry e, CancellationToken ct = default) { Entries.Add(e); return Task.CompletedTask; }
    public Task RemoveEntryAsync(MealPlanEntry e, CancellationToken ct = default) { Entries.Remove(e); return Task.CompletedTask; }
    public Task<IReadOnlyList<Dish>> GetDishesAsync(Guid familyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Dish>>(Dishes.Where(d => d.FamilyId == familyId).ToList());
    public Task<Dish?> GetDishAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Dishes.FirstOrDefault(d => d.Id == id));
    public Task AddDishAsync(Dish d, CancellationToken ct = default) { Dishes.Add(d); return Task.CompletedTask; }
    public Task RemoveDishAsync(Dish d, CancellationToken ct = default) { Dishes.Remove(d); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class MemShopping : IShoppingRepository
{
    public List<ShoppingItem> Items { get; } = [];
    public List<FavoriteItem> Favorites { get; } = [];
    public Task<IReadOnlyList<ShoppingItem>> GetItemsAsync(Guid familyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ShoppingItem>>(Items.Where(i => i.FamilyId == familyId).ToList());
    public Task<ShoppingItem?> GetItemAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task AddItemAsync(ShoppingItem i, CancellationToken ct = default) { Items.Add(i); return Task.CompletedTask; }
    public Task RemoveItemsAsync(IEnumerable<ShoppingItem> items, CancellationToken ct = default) { foreach (var i in items.ToList()) Items.Remove(i); return Task.CompletedTask; }
    public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(Guid familyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FavoriteItem>>(Favorites.Where(f => f.FamilyId == familyId).ToList());
    public Task<FavoriteItem?> GetFavoriteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Favorites.FirstOrDefault(f => f.Id == id));
    public Task AddFavoriteAsync(FavoriteItem f, CancellationToken ct = default) { Favorites.Add(f); return Task.CompletedTask; }
    public Task RemoveFavoriteAsync(FavoriteItem f, CancellationToken ct = default) { Favorites.Remove(f); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class MemCleaning : ICleaningRepository
{
    public List<CleaningTask> Tasks { get; } = [];
    public Task<IReadOnlyList<CleaningTask>> GetAsync(Guid familyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CleaningTask>>(Tasks.Where(t => t.FamilyId == familyId).ToList());
    public Task<CleaningTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id));
    public Task AddAsync(CleaningTask t, CancellationToken ct = default) { Tasks.Add(t); return Task.CompletedTask; }
    public Task RemoveAsync(CleaningTask t, CancellationToken ct = default) { Tasks.Remove(t); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Family with an admin and two children. Time starts on Monday 19 Oct 2026, 14:00 Copenhagen.</summary>
internal sealed class HouseEnv
{
    public Env Base { get; } = new();
    public MemMeals Meals { get; } = new();
    public MemShopping ShoppingRepo { get; } = new();
    public MemCleaning CleaningRepo { get; } = new();
    public ShoppingService Shopping { get; }
    public MealPlanService MealPlan { get; }
    public CleaningService Cleaning { get; }

    public const string Admin = Env.AdminUser, Child = Env.ChildUser, Sibling = Env.SiblingUser;
    public Guid ChildId => Base.Child.Id;
    public Guid SiblingId => Base.Sibling.Id;
    public static readonly DateOnly Monday = new(2026, 10, 19);

    public HouseEnv()
    {
        Shopping = new ShoppingService(Base.Families, ShoppingRepo, Base.Time);
        MealPlan = new MealPlanService(Base.Families, Meals, Shopping);
        Cleaning = new CleaningService(Base.Families, CleaningRepo, Base.Time);
    }

    public void GoToDay(DateOnly d) => Base.Time.UtcNow = new DateTimeOffset(d.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    public static IngredientDto I(string name, string? qty = null) => new(name, qty);

    public static SaveShoppingItemRequest Item(string name, string? qty = null) => new(name, qty, ShoppingCategory.Other, true);
}

public class ShoppingServiceTests
{
    [Fact]
    public async Task Add_guesses_category_and_groups_the_list_with_bought_items_last()
    {
        var env = new HouseEnv();
        await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Mælk", "2 l"));
        await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Tomater"));
        var bread = await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Rugbrød"));
        await env.Shopping.SetBoughtAsync(HouseEnv.Admin, bread.Id, true);

        var list = await env.Shopping.ListAsync(HouseEnv.Child);
        Assert.Equal(["Tomater", "Mælk", "Rugbrød"], list.Select(i => i.Name)); // FruitVeg < Dairy < ... and bought last
        Assert.Equal(ShoppingCategory.FruitVeg, list[0].Category);
        Assert.Equal("2 l", list[1].Quantity);
        Assert.True(list[2].IsBought);
        Assert.Equal("Admin", list[2].BoughtByName);
    }

    [Fact]
    public async Task Chosen_category_is_kept_and_a_duplicate_is_refused()
    {
        var env = new HouseEnv();
        var item = await env.Shopping.AddAsync(HouseEnv.Admin, new SaveShoppingItemRequest("Mælk", null, ShoppingCategory.Household, false));
        Assert.Equal(ShoppingCategory.Household, item.Category);
        await Assert.ThrowsAsync<DomainException>(() => env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("  mælk ")));

        await env.Shopping.SetBoughtAsync(HouseEnv.Admin, item.Id, true);
        await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Mælk")); // buying it again is fine once the first one is bought
    }

    [Fact]
    public async Task Children_can_see_but_not_change_until_admin_allows_it()
    {
        var env = new HouseEnv();
        var item = await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Smør"));
        Assert.Single(await env.Shopping.ListAsync(HouseEnv.Child));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Shopping.AddAsync(HouseEnv.Child, HouseEnv.Item("Ost")));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Shopping.SetBoughtAsync(HouseEnv.Child, item.Id, true));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Shopping.ClearBoughtAsync(HouseEnv.Child));

        env.Base.Family.SetPermission(env.ChildId, Area.Shopping, AccessLevel.Edit);
        await env.Shopping.SetBoughtAsync(HouseEnv.Child, item.Id, true);
        env.Base.Family.SetPermission(env.ChildId, Area.Shopping, AccessLevel.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Shopping.ListAsync(HouseEnv.Child));
    }

    [Fact]
    public async Task Update_unbuy_delete_and_clear_bought()
    {
        var env = new HouseEnv();
        var a = await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Æg"));
        var b = await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Ost"));
        await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Pasta"));

        var changed = await env.Shopping.UpdateAsync(HouseEnv.Admin, a.Id, new SaveShoppingItemRequest("Æg, økologiske", "10 stk.", ShoppingCategory.Other, true));
        Assert.Equal(("Æg, økologiske", "10 stk.", ShoppingCategory.Dairy), (changed.Name, changed.Quantity, changed.Category));

        await env.Shopping.SetBoughtAsync(HouseEnv.Admin, a.Id, true);
        await env.Shopping.SetBoughtAsync(HouseEnv.Admin, b.Id, true);
        await env.Shopping.SetBoughtAsync(HouseEnv.Admin, b.Id, false);
        Assert.Equal(1, await env.Shopping.ClearBoughtAsync(HouseEnv.Admin));
        Assert.Equal(["Ost", "Pasta"], (await env.Shopping.ListAsync(HouseEnv.Admin)).Select(i => i.Name).OrderBy(n => n));

        await env.Shopping.DeleteAsync(HouseEnv.Admin, b.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => env.Shopping.DeleteAsync(HouseEnv.Admin, b.Id));
    }

    [Fact]
    public async Task Favorites_go_on_the_list_with_one_tap_without_duplicates()
    {
        var env = new HouseEnv();
        var fav = await env.Shopping.AddFavoriteAsync(HouseEnv.Admin, HouseEnv.Item("Letmælk", "1 l"));
        Assert.Equal(ShoppingCategory.Dairy, fav.Category);
        await Assert.ThrowsAsync<DomainException>(() => env.Shopping.AddFavoriteAsync(HouseEnv.Admin, HouseEnv.Item("letmælk")));

        Assert.True(await env.Shopping.AddFavoriteToListAsync(HouseEnv.Admin, fav.Id));
        Assert.False(await env.Shopping.AddFavoriteToListAsync(HouseEnv.Admin, fav.Id)); // already there
        var item = (await env.Shopping.ListAsync(HouseEnv.Admin)).Single();
        Assert.Equal(("Letmælk", "1 l"), (item.Name, item.Quantity));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Shopping.AddFavoriteAsync(HouseEnv.Child, HouseEnv.Item("Brød")));
        Assert.Single(await env.Shopping.ListFavoritesAsync(HouseEnv.Child));
        await env.Shopping.DeleteFavoriteAsync(HouseEnv.Admin, fav.Id);
        Assert.Empty(await env.Shopping.ListFavoritesAsync(HouseEnv.Admin));
    }

    [Fact]
    public void Category_suggestion_is_available_to_the_api() =>
        Assert.Equal(ShoppingCategory.Dairy, ShoppingService.SuggestCategory("smør").Category);
}

public class MealPlanServiceTests
{
    private static readonly DateOnly Tuesday = HouseEnv.Monday.AddDays(1);

    private static SaveMealRequest Meal(string title, params IngredientDto[] lines) => new(title, null, null, lines);

    [Fact]
    public async Task Set_replaces_the_dinner_on_a_date_and_get_returns_the_period_in_order()
    {
        var env = new HouseEnv();
        await env.MealPlan.SetAsync(HouseEnv.Admin, Tuesday, Meal("Lasagne"));
        await env.MealPlan.SetAsync(HouseEnv.Admin, HouseEnv.Monday, Meal("Frikadeller"));
        await env.MealPlan.SetAsync(HouseEnv.Admin, Tuesday, Meal("Pasta med kødsovs"));
        await env.MealPlan.SetAsync(HouseEnv.Admin, HouseEnv.Monday.AddDays(20), Meal("Senere"));

        var week = await env.MealPlan.GetAsync(HouseEnv.Child, HouseEnv.Monday, HouseEnv.Monday.AddDays(6));
        Assert.Equal(["Frikadeller", "Pasta med kødsovs"], week.Select(m => m.Title));
        Assert.Equal(3, env.Meals.Entries.Count);

        await env.MealPlan.ClearAsync(HouseEnv.Admin, HouseEnv.Monday);
        await env.MealPlan.ClearAsync(HouseEnv.Admin, HouseEnv.Monday); // clearing an empty day is fine
        Assert.Single(await env.MealPlan.GetAsync(HouseEnv.Admin, HouseEnv.Monday, HouseEnv.Monday.AddDays(6)));
    }

    [Fact]
    public async Task Only_editors_can_change_the_plan_and_the_period_is_limited()
    {
        var env = new HouseEnv();
        await Assert.ThrowsAsync<ForbiddenException>(() => env.MealPlan.SetAsync(HouseEnv.Child, Tuesday, Meal("x")));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.MealPlan.CreateDishAsync(HouseEnv.Child, new SaveDishRequest("x", null)));
        await Assert.ThrowsAsync<DomainException>(() => env.MealPlan.GetAsync(HouseEnv.Admin, Tuesday, Tuesday.AddDays(-1)));
        await Assert.ThrowsAsync<DomainException>(() => env.MealPlan.GetAsync(HouseEnv.Admin, Tuesday, Tuesday.AddDays(400)));
        env.Base.Family.SetPermission(env.ChildId, Area.MealPlan, AccessLevel.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.MealPlan.GetAsync(HouseEnv.Child, Tuesday, Tuesday));
    }

    [Fact]
    public async Task Ingredients_go_on_the_list_without_duplicates()
    {
        var env = new HouseEnv();
        await env.MealPlan.SetAsync(HouseEnv.Admin, HouseEnv.Monday,
            Meal("Lasagne", HouseEnv.I("Hakket oksekød", "500 g"), HouseEnv.I("Tomater", "4"), HouseEnv.I("Ost")));
        await env.MealPlan.SetAsync(HouseEnv.Admin, Tuesday,
            Meal("Pizza", HouseEnv.I("tomater", "2"), HouseEnv.I("Mel", "500 g")));
        await env.Shopping.AddAsync(HouseEnv.Admin, HouseEnv.Item("Ost"));

        var result = await env.MealPlan.AddToShoppingListAsync(HouseEnv.Admin, new AddMealsToShoppingListRequest(HouseEnv.Monday, HouseEnv.Monday.AddDays(6)));
        Assert.Equal(new AddToListResultDto(Added: 3, AlreadyOnList: 2), result); // oksekød, tomater, mel added; ost + second "tomater" skipped
        var names = (await env.Shopping.ListAsync(HouseEnv.Admin)).Select(i => i.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Hakket oksekød", "Mel", "Ost", "Tomater"], names);
        Assert.Equal(ShoppingCategory.Meat, (await env.Shopping.ListAsync(HouseEnv.Admin)).Single(i => i.Name == "Hakket oksekød").Category);

        var again = await env.MealPlan.AddToShoppingListAsync(HouseEnv.Admin, new AddMealsToShoppingListRequest(HouseEnv.Monday, HouseEnv.Monday.AddDays(6)));
        Assert.Equal(0, again.Added);
    }

    [Fact]
    public async Task Adding_to_the_list_needs_shopping_edit_rights()
    {
        var env = new HouseEnv();
        await env.MealPlan.SetAsync(HouseEnv.Admin, HouseEnv.Monday, Meal("Suppe", HouseEnv.I("Gulerødder")));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            env.MealPlan.AddToShoppingListAsync(HouseEnv.Child, new AddMealsToShoppingListRequest(HouseEnv.Monday, HouseEnv.Monday)));
        await Assert.ThrowsAsync<DomainException>(() =>
            env.MealPlan.AddToShoppingListAsync(HouseEnv.Admin, new AddMealsToShoppingListRequest(HouseEnv.Monday, HouseEnv.Monday.AddDays(60))));
    }

    [Fact]
    public async Task Save_as_dish_then_reuse_it_and_keep_day_changes_separate()
    {
        var env = new HouseEnv();
        await env.MealPlan.SetAsync(HouseEnv.Admin, HouseEnv.Monday, Meal("Lasagne", HouseEnv.I("Pasta"), HouseEnv.I("Ost")));
        var dish = await env.MealPlan.SaveAsDishAsync(HouseEnv.Admin, HouseEnv.Monday);
        Assert.Equal(2, dish.Ingredients.Count);
        Assert.Equal(dish.Id, (await env.MealPlan.GetAsync(HouseEnv.Admin, HouseEnv.Monday, HouseEnv.Monday)).Single().DishId);

        // Planning it again copies the ingredients; editing them on that day does not change the dish.
        await env.MealPlan.SetAsync(HouseEnv.Admin, Tuesday, new SaveMealRequest("Lasagne", null, dish.Id, dish.Ingredients.Append(HouseEnv.I("Salat")).ToList()));
        Assert.Equal(2, (await env.MealPlan.ListDishesAsync(HouseEnv.Admin)).Single().Ingredients.Count);

        // Saving again updates the same dish instead of making a second one.
        var again = await env.MealPlan.SaveAsDishAsync(HouseEnv.Admin, Tuesday);
        Assert.Equal(dish.Id, again.Id);
        Assert.Equal(3, again.Ingredients.Count);
        Assert.Single(await env.MealPlan.ListDishesAsync(HouseEnv.Child));

        await Assert.ThrowsAsync<NotFoundException>(() => env.MealPlan.SaveAsDishAsync(HouseEnv.Admin, HouseEnv.Monday.AddDays(5)));
    }

    [Fact]
    public async Task Dish_crud_and_unique_names()
    {
        var env = new HouseEnv();
        var dish = await env.MealPlan.CreateDishAsync(HouseEnv.Admin, new SaveDishRequest("Chili con carne", [HouseEnv.I("Bønner")]));
        await Assert.ThrowsAsync<DomainException>(() => env.MealPlan.CreateDishAsync(HouseEnv.Admin, new SaveDishRequest(" chili CON carne ", null)));
        var changed = await env.MealPlan.UpdateDishAsync(HouseEnv.Admin, dish.Id, new SaveDishRequest("Chili", [HouseEnv.I("Bønner"), HouseEnv.I("Ris")]));
        Assert.Equal(("Chili", 2), (changed.Name, changed.Ingredients.Count));

        // A day that points at a deleted dish keeps its own copy of the ingredients.
        await env.MealPlan.SetAsync(HouseEnv.Admin, HouseEnv.Monday, new SaveMealRequest("Chili", null, dish.Id, changed.Ingredients));
        await env.MealPlan.DeleteDishAsync(HouseEnv.Admin, dish.Id);
        Assert.Equal(2, (await env.MealPlan.GetAsync(HouseEnv.Admin, HouseEnv.Monday, HouseEnv.Monday)).Single().Ingredients.Count);
        await Assert.ThrowsAsync<NotFoundException>(() => env.MealPlan.DeleteDishAsync(HouseEnv.Admin, dish.Id));

        // A dish id from nowhere is forgotten rather than stored.
        var meal = await env.MealPlan.SetAsync(HouseEnv.Admin, Tuesday, new SaveMealRequest("Noget", null, Guid.NewGuid(), null));
        Assert.Null(meal.DishId);
    }
}

public class CleaningServiceTests
{
    private static SaveCleaningRequest Task(string name, int days = 7, Guid? who = null, string? room = "Stue") => new(name, room, days, who);

    [Fact]
    public async Task Lists_by_room_with_status_and_due_dates()
    {
        var env = new HouseEnv();
        env.GoToDay(HouseEnv.Monday);
        var hoover = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Støvsug stuen", 7));
        var bath = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Gør badeværelset rent", 14, room: "Badeværelse"));
        Assert.Equal(CleaningStatus.NeverDone, hoover.Status);

        await env.Cleaning.MarkDoneAsync(HouseEnv.Admin, hoover.Id, new MarkCleaningDoneRequest(HouseEnv.Monday.AddDays(-9)));
        await env.Cleaning.MarkDoneAsync(HouseEnv.Admin, bath.Id, null);

        var list = await env.Cleaning.ListAsync(HouseEnv.Admin);
        Assert.Equal(["Gør badeværelset rent", "Støvsug stuen"], list.Select(t => t.Name)); // by room: Badeværelse, Stue
        var b = list[0];
        Assert.Equal((CleaningStatus.Upcoming, HouseEnv.Monday.AddDays(14), -14), (b.Status, b.NextDue, b.DaysOverdue));
        Assert.Equal("Admin", b.LastDoneByName);
        var h = list[1];
        Assert.Equal((CleaningStatus.Overdue, 2), (h.Status, h.DaysOverdue)); // last done 9 days ago, every 7 days

        env.GoToDay(HouseEnv.Monday.AddDays(14));
        Assert.Equal(CleaningStatus.DueToday, (await env.Cleaning.ListAsync(HouseEnv.Admin)).First(t => t.Name.StartsWith("Gør")).Status);
    }

    [Fact]
    public async Task Child_marks_own_or_unowned_jobs_but_cannot_edit_or_mark_someone_elses()
    {
        var env = new HouseEnv();
        env.GoToDay(HouseEnv.Monday);
        var own = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Tøm skraldespanden", 3, env.ChildId, "Køkken"));
        var theirs = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Ryd Noahs værelse", 7, env.SiblingId, "Værelser"));
        var nobodys = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Tør støv af", 7, null, "Stue"));

        var seenByChild = await env.Cleaning.ListAsync(HouseEnv.Child);
        Assert.True(seenByChild.Single(t => t.Id == own.Id).CanMarkDone);
        Assert.False(seenByChild.Single(t => t.Id == theirs.Id).CanMarkDone);
        Assert.True(seenByChild.Single(t => t.Id == nobodys.Id).CanMarkDone);

        await env.Cleaning.MarkDoneAsync(HouseEnv.Child, own.Id, null);
        await env.Cleaning.MarkDoneAsync(HouseEnv.Child, nobodys.Id, null);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Cleaning.MarkDoneAsync(HouseEnv.Child, theirs.Id, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Cleaning.CreateAsync(HouseEnv.Child, Task("x")));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Cleaning.DeleteAsync(HouseEnv.Child, own.Id));
    }

    [Fact]
    public async Task Date_rules_are_enforced()
    {
        var env = new HouseEnv();
        env.GoToDay(HouseEnv.Monday);
        var task = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Vask vinduer", 30));
        await Assert.ThrowsAsync<DomainException>(() => env.Cleaning.MarkDoneAsync(HouseEnv.Admin, task.Id, new MarkCleaningDoneRequest(HouseEnv.Monday.AddDays(1))));
        await Assert.ThrowsAsync<DomainException>(() => env.Cleaning.MarkDoneAsync(HouseEnv.Admin, task.Id, new MarkCleaningDoneRequest(HouseEnv.Monday.AddDays(-20))));
        var done = await env.Cleaning.MarkDoneAsync(HouseEnv.Admin, task.Id, new MarkCleaningDoneRequest(HouseEnv.Monday.AddDays(-1)));
        Assert.Equal(HouseEnv.Monday.AddDays(-1), done.LastDoneOn);
    }

    [Fact]
    public async Task Update_delete_and_validation()
    {
        var env = new HouseEnv();
        var task = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("A"));
        var changed = await env.Cleaning.UpdateAsync(HouseEnv.Admin, task.Id, new SaveCleaningRequest("B", " ", 30, env.ChildId));
        Assert.Equal(("B", "Andet", 30, "Emma"), (changed.Name, changed.Room, changed.IntervalDays, changed.AssigneeName));
        await Assert.ThrowsAsync<DomainException>(() => env.Cleaning.CreateAsync(HouseEnv.Admin, Task("x", 0)));
        await Assert.ThrowsAsync<DomainException>(() => env.Cleaning.CreateAsync(HouseEnv.Admin, Task("x", 7, Guid.NewGuid())));

        await env.Cleaning.DeleteAsync(HouseEnv.Admin, task.Id);
        Assert.Empty(env.CleaningRepo.Tasks);
        await Assert.ThrowsAsync<NotFoundException>(() => env.Cleaning.MarkDoneAsync(HouseEnv.Admin, task.Id, null));
    }

    [Fact]
    public async Task Without_area_access_only_own_jobs_are_visible()
    {
        var env = new HouseEnv();
        await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Fælles", 7));
        var mine = await env.Cleaning.CreateAsync(HouseEnv.Admin, Task("Min", 7, env.ChildId));
        env.Base.Family.SetPermission(env.ChildId, Area.Cleaning, AccessLevel.None);
        Assert.Equal([mine.Id], (await env.Cleaning.ListAsync(HouseEnv.Child)).Select(t => t.Id));
    }
}
