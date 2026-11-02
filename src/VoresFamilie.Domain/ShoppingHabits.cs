namespace VoresFamilie.Domain;

/// <summary>One purchase of an item on a given day. Kept after the item leaves the list, so habits can be seen over time.</summary>
public sealed class PurchaseRecord
{
    private PurchaseRecord() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    /// <summary>The list item that was ticked off; used to undo the record when the tick is taken back.</summary>
    public Guid ItemId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Quantity { get; private set; }
    public ShoppingCategory Category { get; private set; }
    public DateOnly BoughtOn { get; private set; }

    public static PurchaseRecord For(ShoppingItem item, DateOnly boughtOn) => new()
    {
        Id = Guid.NewGuid(),
        FamilyId = item.FamilyId,
        ItemId = item.Id,
        Name = item.Name,
        Quantity = item.Quantity,
        Category = item.Category,
        BoughtOn = boughtOn,
    };
}

/// <summary>An item that is bought regularly and is not on the list right now.</summary>
public sealed record ShoppingHabit(string Name, string? Quantity, ShoppingCategory Category, int TimesBought, int AverageDays, int DaysSinceLast);

/// <summary>Works out which items the family buys about once a week from the purchase history.</summary>
public static class ShoppingHabits
{
    /// <summary>How far back purchases count.</summary>
    public const int WindowDays = 56;
    /// <summary>Different shopping days needed within the window before something counts as a habit.</summary>
    public const int MinTimesBought = 3;
    /// <summary>Bought at least this often on average (a week, with some slack).</summary>
    public const int MaxAverageDays = 10;
    /// <summary>Not suggested again right after it was bought.</summary>
    public const int MinDaysSinceLast = 4;
    /// <summary>A habit that has not been seen for this long is considered dropped.</summary>
    public const int MaxDaysSinceLast = 21;

    public static IReadOnlyList<ShoppingHabit> Suggest(IEnumerable<PurchaseRecord> purchases, IEnumerable<string> namesOnList, DateOnly today)
    {
        var onList = new HashSet<string>(namesOnList.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
        var since = today.AddDays(-WindowDays);
        var habits = new List<ShoppingHabit>();

        foreach (var group in purchases.Where(p => p.BoughtOn >= since && p.BoughtOn <= today)
                     .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (onList.Contains(group.Key)) continue;

            var days = group.Select(p => p.BoughtOn).Distinct().Order().ToList();
            if (days.Count < MinTimesBought) continue;

            var average = (double)(days[^1].DayNumber - days[0].DayNumber) / (days.Count - 1);
            var sinceLast = today.DayNumber - days[^1].DayNumber;
            if (average > MaxAverageDays || sinceLast < MinDaysSinceLast || sinceLast > MaxDaysSinceLast) continue;

            // Name, amount and department as it was bought most recently.
            var latest = group.OrderByDescending(p => p.BoughtOn).First();
            habits.Add(new ShoppingHabit(latest.Name, latest.Quantity, latest.Category, days.Count, (int)Math.Round(average), sinceLast));
        }

        return habits.OrderBy(h => h.Category).ThenBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
