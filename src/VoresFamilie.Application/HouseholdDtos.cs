using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed record IngredientDto(string Name, string? Quantity);

public sealed record MealDto(DateOnly Date, string Title, string? Notes, Guid? DishId, IReadOnlyList<IngredientDto> Ingredients);

public sealed record SaveMealRequest(string Title, string? Notes, Guid? DishId, IReadOnlyList<IngredientDto>? Ingredients);

public sealed record DishDto(Guid Id, string Name, IReadOnlyList<IngredientDto> Ingredients);

public sealed record SaveDishRequest(string Name, IReadOnlyList<IngredientDto>? Ingredients);

/// <summary>Both dates are included.</summary>
public sealed record AddMealsToShoppingListRequest(DateOnly From, DateOnly To);

public sealed record AddToListResultDto(int Added, int AlreadyOnList);

public sealed record ShoppingItemDto(Guid Id, string Name, string? Quantity, ShoppingCategory Category, bool IsBought,
    DateTimeOffset? BoughtAt, string? BoughtByName, string? AddedByName);

/// <summary>AutoCategory = true means "work the department out from the name" (the Category value is then ignored).</summary>
public sealed record SaveShoppingItemRequest(string Name, string? Quantity, ShoppingCategory Category, bool AutoCategory);

public sealed record CategorySuggestionDto(ShoppingCategory Category);

/// <summary>Something the family buys about every week that is not on the list now.</summary>
public sealed record HabitDto(string Name, string? Quantity, ShoppingCategory Category, int TimesBought, int AverageDays, int DaysSinceLast);

public sealed record FavoriteDto(Guid Id, string Name, string? Quantity, ShoppingCategory Category);

public sealed record CleaningDto(Guid Id, string Name, string Room, int IntervalDays, Guid? AssigneeMemberId, string? AssigneeName,
    DateOnly? LastDoneOn, string? LastDoneByName, DateOnly NextDue, int DaysOverdue, CleaningStatus Status, bool CanMarkDone);

public sealed record SaveCleaningRequest(string Name, string? Room, int IntervalDays, Guid? AssigneeMemberId);

/// <summary>Date: when it was done (default today, at most 14 days back).</summary>
public sealed record MarkCleaningDoneRequest(DateOnly? Date);
