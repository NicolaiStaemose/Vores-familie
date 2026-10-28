using VoresFamilie.Domain;

namespace VoresFamilie.Domain.Tests;

public class ShoppingHabitTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);

    private static PurchaseRecord Bought(string name, int daysAgo, string? qty = null, ShoppingCategory? category = null)
    {
        var item = ShoppingItem.Create(Guid.NewGuid(), name, qty, category, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return PurchaseRecord.For(item, Today.AddDays(-daysAgo));
    }

    private static List<string> Names(IEnumerable<PurchaseRecord> purchases, params string[] onList) =>
        ShoppingHabits.Suggest(purchases, onList, Today).Select(h => h.Name).ToList();

    [Fact]
    public void Item_bought_every_week_is_suggested()
    {
        Assert.Equal(["Mælk"], Names([Bought("Mælk", 5), Bought("Mælk", 12), Bought("Mælk", 19), Bought("Mælk", 26)]));
    }

    [Fact]
    public void Too_few_purchases_is_not_a_habit()
    {
        Assert.Empty(Names([Bought("Mælk", 5), Bought("Mælk", 12)]));
    }

    [Fact]
    public void Rare_purchases_are_not_a_habit()
    {
        Assert.Empty(Names([Bought("Kaffe", 10), Bought("Kaffe", 30), Bought("Kaffe", 50)])); // every 20 days
    }

    [Fact]
    public void Item_bought_a_moment_ago_waits_a_few_days()
    {
        var weekly = new[] { Bought("Æg", 2), Bought("Æg", 9), Bought("Æg", 16) };
        Assert.Empty(Names(weekly));
        Assert.Equal(["Æg"], ShoppingHabits.Suggest(weekly, [], Today.AddDays(2)).Select(h => h.Name));
    }

    [Fact]
    public void Item_already_on_the_list_is_not_suggested_whatever_the_casing()
    {
        var weekly = new[] { Bought("Mælk", 5), Bought("Mælk", 12), Bought("Mælk", 19) };
        Assert.Empty(Names(weekly, " mælk "));
    }

    [Fact]
    public void Habit_that_stopped_is_dropped()
    {
        Assert.Empty(Names([Bought("Boller", 25), Bought("Boller", 32), Bought("Boller", 39)]));
    }

    [Fact]
    public void Old_purchases_outside_the_window_do_not_count()
    {
        Assert.Empty(Names([Bought("Yoghurt", 5), Bought("Yoghurt", 70), Bought("Yoghurt", 77)]));
    }

    [Fact]
    public void Several_purchases_on_the_same_day_count_once()
    {
        Assert.Empty(Names([Bought("Mælk", 5), Bought("Mælk", 5), Bought("Mælk", 5)]));
    }

    [Fact]
    public void Latest_amount_and_category_are_used_and_the_result_is_grouped_by_department()
    {
        var result = ShoppingHabits.Suggest(
        [
            Bought("Rugbrød", 6), Bought("Rugbrød", 13), Bought("Rugbrød", 20),
            Bought("Mælk", 5, "1 l"), Bought("Mælk", 12, "2 l"), Bought("Mælk", 19, "2 l"),
        ], [], Today);
        Assert.Equal(["Mælk", "Rugbrød"], result.Select(h => h.Name)); // dairy before bread
        Assert.Equal("1 l", result[0].Quantity);
        Assert.Equal((3, 7, 5), (result[0].TimesBought, result[0].AverageDays, result[0].DaysSinceLast));
    }
}
