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

    public static DateTimeOffset Local(DateTime localDateTime) =>
        new(DateTime.SpecifyKind(localDateTime, DateTimeKind.Local));
}
