using VoresFamilie.Domain;

namespace VoresFamilie.Application;

/// <summary>Days are a bit mask: Monday=1, Tuesday=2, Wednesday=4, Thursday=8, Friday=16, Saturday=32, Sunday=64 (127 = every day).</summary>
/// <summary>Which routine a point belongs to. None for household tasks. (A non-nullable enum keeps the generated client simple.)</summary>
public enum RoutineSlot { None, Morning, Evening }

public sealed record ChecklistItemDto(Guid Id, ChecklistKind Kind, RoutineSlot Part, Guid? MemberId, string? MemberName,
    string Title, int Days, int SortOrder);

public sealed record SaveChecklistItemRequest(ChecklistKind Kind, RoutineSlot Part, Guid? MemberId, string Title, int Days);

public sealed record MoveChecklistItemRequest(bool Up);

public sealed record ChecklistEntryDto(Guid ItemId, ChecklistKind Kind, RoutineSlot Part, string Title, Guid? MemberId,
    string? MemberName, bool Done, DateTimeOffset? CompletedAt, string? CompletedByName, bool CanCheck);

public sealed record ChecklistDayDto(DateOnly Date, Guid MemberId, string MemberName, bool CanTickDate, IReadOnlyList<ChecklistEntryDto> Entries);

public enum TodoFilter { All, Mine, Open }

public sealed record TodoDto(Guid Id, string Title, string? Notes, Guid? AssigneeMemberId, string? AssigneeName, DateOnly? DueDate,
    bool IsDone, DateTimeOffset? CompletedAt, string? CompletedByName, bool IsOverdue, bool CanCheck);

public sealed record SaveTodoRequest(string Title, string? Notes, Guid? AssigneeMemberId, DateOnly? DueDate);

