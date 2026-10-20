using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed record CalendarDto(Guid Id, string Name, CalendarKind Kind, Guid? OwnerMemberId, string? OwnerName,
    string Color, DateTimeOffset? LastSyncedAt, bool CanEdit);

public sealed record GoogleStatusDto(bool Configured, bool Connected);

public sealed record GoogleCalendarDto(string Id, string Name, string? Color, bool AlreadyAdded);

public sealed record AddCalendarRequest(string GoogleCalendarId, string Name, CalendarKind Kind, Guid? OwnerMemberId, string? Color);

public sealed record EventDto(string Id, Guid CalendarId, string Title, string? Description, string? Location,
    DateTimeOffset Start, DateTimeOffset End, bool AllDay, bool IsHomework, string? Subject);

public sealed record SaveEventRequest(string Title, string? Description, string? Location,
    DateTimeOffset Start, DateTimeOffset End, bool AllDay);

public sealed record HomeworkItemDto(Guid CalendarId, string EventId, string Title, string? Description, string? Subject,
    DateTimeOffset Start, bool AllDay, Guid ChildMemberId, string ChildName, bool Done, DateTimeOffset? CompletedAt, bool CanCheck);

public sealed record SyncResultDto(int Calendars, int Failed);
