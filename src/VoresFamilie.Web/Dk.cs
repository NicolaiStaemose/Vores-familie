using VoresFamilie.Web.Api;

namespace VoresFamilie.Web;

/// <summary>Danish date/time text. Written by hand so it works without browser globalization data.</summary>
public static class Dk
{
    private static readonly string[] Days = ["søndag", "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag"];
    private static readonly string[] Months = ["jan.", "feb.", "mar.", "apr.", "maj", "jun.", "jul.", "aug.", "sep.", "okt.", "nov.", "dec."];

    public static string Day(DateTime d) => $"{Days[(int)d.DayOfWeek]} {d.Day}. {Months[d.Month - 1]}";

    public static string DayShort(DateTime d) => $"{d.Day}. {Months[d.Month - 1]}";

    public static string Time(DateTimeOffset t) => t.LocalDateTime.ToString("HH:mm");

    public static string DateTimeText(DateTimeOffset t) => $"{DayShort(t.LocalDateTime)} kl. {Time(t)}";

    /// <summary>Monday of the week containing <paramref name="d"/>.</summary>
    public static DateTime WeekStart(DateTime d)
    {
        var diff = ((int)d.DayOfWeek + 6) % 7;
        return d.Date.AddDays(-diff);
    }

    /// <summary>ISO week number.</summary>
    public static int Week(DateTime d) => System.Globalization.ISOWeek.GetWeekOfYear(d);

    public static readonly string[] DayShortNames = ["man", "tir", "ons", "tor", "fre", "lør", "søn"];

    /// <summary>Text for a weekday bit mask (Monday=1 ... Sunday=64).</summary>
    public static string DaysText(int mask)
    {
        if (mask == 127) return "Alle dage";
        if (mask == 31) return "Hverdage";
        if (mask == 96) return "Weekend";
        return string.Join(", ", DayShortNames.Where((_, i) => (mask & (1 << i)) != 0));
    }

    public static string CategoryName(ShoppingCategory c) => c switch
    {
        ShoppingCategory.FruitVeg => "Frugt og grønt",
        ShoppingCategory.Dairy => "Mejeri",
        ShoppingCategory.Meat => "Kød og fisk",
        ShoppingCategory.Bread => "Brød",
        ShoppingCategory.Pantry => "Kolonial",
        ShoppingCategory.Frozen => "Frost",
        ShoppingCategory.Drinks => "Drikkevarer",
        ShoppingCategory.Household => "Husholdning",
        _ => "Andet",
    };

    /// <summary>The order of the departments on a trip through the shop.</summary>
    public static readonly ShoppingCategory[] CategoryOrder =
    [
        ShoppingCategory.FruitVeg, ShoppingCategory.Bread, ShoppingCategory.Dairy, ShoppingCategory.Meat, ShoppingCategory.Pantry,
        ShoppingCategory.Drinks, ShoppingCategory.Frozen, ShoppingCategory.Household, ShoppingCategory.Other,
    ];

    /// <summary>"i dag", "i går", "for 3 dage siden" or the date.</summary>
    public static string DaysAgo(DateOnly date, DateOnly today)
    {
        var diff = today.DayNumber - date.DayNumber;
        return diff switch
        {
            0 => "i dag",
            1 => "i går",
            > 1 and < 14 => $"for {diff} dage siden",
            _ => DayShort(date.ToDateTime(TimeOnly.MinValue)),
        };
    }

    public static DateTimeOffset Local(DateTime localDateTime) =>
        new(DateTime.SpecifyKind(localDateTime, DateTimeKind.Local));
}



