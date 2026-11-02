using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class ShoppingCategoryTests
{
    [Theory]
    [InlineData("Mælk", ShoppingCategory.Dairy)]
    [InlineData("Letmælk 1 l", ShoppingCategory.Dairy)]
    [InlineData("Æg", ShoppingCategory.Dairy)]
    [InlineData("Smør", ShoppingCategory.Dairy)]
    [InlineData("Revet ost", ShoppingCategory.Dairy)]
    [InlineData("Tomater", ShoppingCategory.FruitVeg)]
    [InlineData("Agurk", ShoppingCategory.FruitVeg)]
    [InlineData("Peberfrugt", ShoppingCategory.FruitVeg)]
    [InlineData("Bananer", ShoppingCategory.FruitVeg)]
    [InlineData("Melon", ShoppingCategory.Other)]
    [InlineData("Kylling", ShoppingCategory.Meat)]
    [InlineData("Hakket oksekød", ShoppingCategory.Meat)]
    [InlineData("Laks", ShoppingCategory.Meat)]
    [InlineData("Rugbrød", ShoppingCategory.Bread)]
    [InlineData("Tortillas", ShoppingCategory.Bread)]
    [InlineData("Pasta", ShoppingCategory.Pantry)]
    [InlineData("Ris", ShoppingCategory.Pantry)]
    [InlineData("Hvedemel", ShoppingCategory.Pantry)]
    [InlineData("Hakkede tomater", ShoppingCategory.Pantry)]
    [InlineData("Flåede tomater på dåse", ShoppingCategory.Pantry)]
    [InlineData("Peber", ShoppingCategory.Pantry)]
    [InlineData("Frosne ærter", ShoppingCategory.Frozen)]
    [InlineData("Pizza", ShoppingCategory.Frozen)]
    [InlineData("Is", ShoppingCategory.Frozen)]
    [InlineData("Frost", ShoppingCategory.Frozen)]
    [InlineData("Appelsinjuice", ShoppingCategory.Drinks)]
    [InlineData("Danskvand", ShoppingCategory.Drinks)]
    [InlineData("Vand", ShoppingCategory.Drinks)]
    [InlineData("Vandmelon", ShoppingCategory.Other)]
    [InlineData("Vindruer", ShoppingCategory.FruitVeg)]
    [InlineData("Toiletpapir", ShoppingCategory.Household)]
    [InlineData("Opvaskemiddel", ShoppingCategory.Household)]
    [InlineData("Noget helt andet", ShoppingCategory.Other)]
    [InlineData("", ShoppingCategory.Other)]
    [InlineData(null, ShoppingCategory.Other)]
    public void Suggests_a_department_from_the_name(string? name, ShoppingCategory expected) =>
        Assert.Equal(expected, ShoppingCategories.Suggest(name));
}

public class MealAndShoppingModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ingredients_are_cleaned()
    {
        var clean = IngredientLine.Clean([new(" Tomater ", " 400 g "), new("", "1"), new("  ", null), new("Løg", " ")]);
        Assert.Equal([new IngredientLine("Tomater", "400 g"), new IngredientLine("Løg", null)], clean);
        Assert.Empty(IngredientLine.Clean(null));
        Assert.Equal(IngredientLine.MaxLines, IngredientLine.Clean(Enumerable.Range(0, 200).Select(i => new IngredientLine($"v{i}", null))).Count);
    }

    [Fact]
    public void Dish_and_meal_need_a_name()
    {
        Assert.Throws<DomainException>(() => Dish.Create(Guid.NewGuid(), " ", null));
        Assert.Throws<DomainException>(() => MealPlanEntry.Create(Guid.NewGuid(), new DateOnly(2026, 10, 20), "", null, null, null));
        var meal = MealPlanEntry.Create(Guid.NewGuid(), new DateOnly(2026, 10, 20), " Lasagne ", " ", null, [new("Pasta", null)]);
        Assert.Equal("Lasagne", meal.Title);
        Assert.Null(meal.Notes);
        Assert.Single(meal.Ingredients);
    }

    [Fact]
    public void Shopping_item_gets_its_category_from_the_name_unless_chosen()
    {
        var auto = ShoppingItem.Create(Guid.NewGuid(), "Mælk", " 2 l ", null, Guid.NewGuid(), Now);
        Assert.Equal(ShoppingCategory.Dairy, auto.Category);
        Assert.Equal("2 l", auto.Quantity);
        var chosen = ShoppingItem.Create(Guid.NewGuid(), "Mælk", null, ShoppingCategory.Household, Guid.NewGuid(), Now);
        Assert.Equal(ShoppingCategory.Household, chosen.Category);
        Assert.Throws<DomainException>(() => ShoppingItem.Create(Guid.NewGuid(), " ", null, null, Guid.NewGuid(), Now));
    }

    [Fact]
    public void Buying_is_idempotent_and_can_be_undone()
    {
        var who = Guid.NewGuid();
        var item = ShoppingItem.Create(Guid.NewGuid(), "Smør", null, null, who, Now);
        item.Buy(who, Now.AddHours(1));
        item.Buy(Guid.NewGuid(), Now.AddHours(2));
        Assert.True(item.IsBought);
        Assert.Equal(Now.AddHours(1), item.BoughtAt);
        item.Unbuy();
        Assert.False(item.IsBought);
        Assert.True(item.HasName("  SMØR "));
    }

    [Fact]
    public void Favorite_needs_a_name_and_guesses_the_category()
    {
        Assert.Throws<DomainException>(() => FavoriteItem.Create(Guid.NewGuid(), "", null, null));
        Assert.Equal(ShoppingCategory.Bread, FavoriteItem.Create(Guid.NewGuid(), "Rugbrød", null, null).Category);
    }

    [Fact]
    public void Meal_and_shopping_rules_follow_the_areas()
    {
        var f = Family.Create("F", "u-admin", "Admin");
        var child = f.AcceptInvitation(f.Invite(Role.Barn, null, Now), "u-c", "Emma", Now);
        Assert.True(MealRules.CanView(child));
        Assert.False(MealRules.CanEdit(child));
        Assert.True(ShoppingRules.CanView(child));
        Assert.False(ShoppingRules.CanEdit(child));
        f.SetPermission(child.Id, Area.Shopping, AccessLevel.Edit);
        Assert.True(ShoppingRules.CanEdit(child));
        f.SetPermission(child.Id, Area.MealPlan, AccessLevel.None);
        Assert.False(MealRules.CanView(child));
        Assert.True(MealRules.CanEdit(f.Members[0]));
    }
}

public class CleaningTaskTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static CleaningTask NewTask(int interval = 7, Guid? assignee = null) =>
        CleaningTask.Create(Guid.NewGuid(), "Støvsug stuen", "Stue", interval, assignee, Today.AddDays(-30));

    [Fact]
    public void Never_done_task_is_due_from_the_day_it_was_created()
    {
        var task = NewTask();
        Assert.Equal(CleaningStatus.NeverDone, task.StatusOn(Today));
        Assert.Equal(Today.AddDays(-30), task.NextDue);
        Assert.True(task.NeedsAttention(Today));
    }

    [Fact]
    public void Status_follows_last_done_plus_interval()
    {
        var task = NewTask(interval: 7);
        var who = Guid.NewGuid();
        task.MarkDone(Today.AddDays(-10), who, Today);
        Assert.Equal(CleaningStatus.Overdue, task.StatusOn(Today));
        Assert.Equal(3, task.DaysOverdue(Today));

        task.MarkDone(Today.AddDays(-7), who, Today);
        Assert.Equal(CleaningStatus.DueToday, task.StatusOn(Today));
        Assert.Equal(0, task.DaysOverdue(Today));

        task.MarkDone(Today, who, Today);
        Assert.Equal(CleaningStatus.Upcoming, task.StatusOn(Today));
        Assert.Equal(Today.AddDays(7), task.NextDue);
        Assert.False(task.NeedsAttention(Today));
        Assert.Equal(-7, task.DaysOverdue(Today));
        Assert.Equal(who, task.LastDoneByMemberId);
    }

    [Fact]
    public void Done_date_must_be_recent_and_not_in_the_future_or_older_than_the_last_one()
    {
        var task = NewTask();
        var who = Guid.NewGuid();
        Assert.Throws<DomainException>(() => task.MarkDone(Today.AddDays(1), who, Today));
        Assert.Throws<DomainException>(() => task.MarkDone(Today.AddDays(-CleaningTask.MaxDaysBack - 1), who, Today));
        task.MarkDone(Today.AddDays(-CleaningTask.MaxDaysBack), who, Today); // exactly the limit is fine
        task.MarkDone(Today.AddDays(-2), who, Today);
        Assert.Throws<DomainException>(() => task.MarkDone(Today.AddDays(-5), who, Today));
    }

    [Theory]
    [InlineData("", 7)]
    [InlineData("x", 0)]
    [InlineData("x", 366)]
    public void Name_and_interval_are_validated(string name, int interval) =>
        Assert.Throws<DomainException>(() => CleaningTask.Create(Guid.NewGuid(), name, "Køkken", interval, null, Today));

    [Fact]
    public void Room_defaults_to_other() =>
        Assert.Equal("Andet", CleaningTask.Create(Guid.NewGuid(), "Vinduer", " ", 30, null, Today).Room);

    [Fact]
    public void Rules_for_who_may_mark_it_done()
    {
        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var f = Family.Create("F", "u-admin", "Admin");
        var child = f.AcceptInvitation(f.Invite(Role.Barn, null, now), "u-c", "Emma", now);
        var sibling = f.AcceptInvitation(f.Invite(Role.Barn, null, now), "u-s", "Noah", now);

        var own = NewTask(assignee: child.Id);
        var nobodys = NewTask();
        Assert.True(CleaningRules.CanMarkDone(child, own));
        Assert.False(CleaningRules.CanMarkDone(sibling, own));
        Assert.True(CleaningRules.CanMarkDone(sibling, nobodys));
        Assert.True(CleaningRules.CanMarkDone(f.Members[0], own)); // admin edits
        Assert.False(CleaningRules.CanEdit(child));

        f.SetCanCheckOwnItems(child.Id, false);
        Assert.False(CleaningRules.CanMarkDone(child, own));

        f.SetPermission(sibling.Id, Area.Cleaning, AccessLevel.None);
        Assert.False(CleaningRules.CanMarkDone(sibling, nobodys));
        Assert.True(CleaningRules.CanView(child, own));
    }
}
