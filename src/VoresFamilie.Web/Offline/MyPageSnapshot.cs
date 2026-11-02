using VoresFamilie.Web.Api;

namespace VoresFamilie.Web;

/// <summary>What "Min side" showed the last time it was loaded for the signed-in person, so it can be seen without a connection.</summary>
public sealed class MyPageSnapshot
{
    public DateOnly Date { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public List<CalendarDto> Calendars { get; set; } = [];
    public List<EventDto> Events { get; set; } = [];
    public List<HomeworkItemDto> Homework { get; set; } = [];
    public ChecklistDayDto? Checklist { get; set; }
    public MealDto? Meal { get; set; }
    public List<CleaningDto> CleaningDue { get; set; } = [];
    public List<TodoDto> Todos { get; set; } = [];

    /// <summary>
    /// A saved copy can only be looked at: nothing in it may be ticked off, because the change could not be sent
    /// (only the shopping list has a queue for that).
    /// </summary>
    public void MakeReadOnly()
    {
        if (Checklist is not null)
        {
            Checklist.CanTickDate = false;
            foreach (var entry in Checklist.Entries) entry.CanCheck = false;
        }
        foreach (var todo in Todos) todo.CanCheck = false;
        foreach (var task in CleaningDue) task.CanMarkDone = false;
        foreach (var homework in Homework) homework.CanCheck = false;
    }
}
