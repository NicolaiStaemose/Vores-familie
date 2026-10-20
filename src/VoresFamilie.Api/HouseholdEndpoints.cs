using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using VoresFamilie.Application;

namespace VoresFamilie.Api;

public static class HouseholdEndpoints
{
    public static void MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").RequireAuthorization().WithTags("Household");

        // ---- Meal plan ----
        g.MapGet("/meals", async (DateOnly from, DateOnly to, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            await s.GetAsync(u.GetUserId(p)!, from, to, ct))
            .WithName("ListMeals").Produces<IReadOnlyList<MealDto>>();

        g.MapPut("/meals/{date}", async (DateOnly date, SaveMealRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            Results.Ok(await s.SetAsync(u.GetUserId(p)!, date, r, ct)))
            .WithName("SetMeal").Produces<MealDto>();

        g.MapDelete("/meals/{date}", async (DateOnly date, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
        { await s.ClearAsync(u.GetUserId(p)!, date, ct); return Results.NoContent(); })
            .WithName("ClearMeal").Produces(204);

        g.MapPost("/meals/to-shopping-list", async (AddMealsToShoppingListRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            Results.Ok(await s.AddToShoppingListAsync(u.GetUserId(p)!, r, ct)))
            .WithName("AddMealsToShoppingList").Produces<AddToListResultDto>();

        g.MapPost("/meals/{date}/save-as-dish", async (DateOnly date, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            Results.Ok(await s.SaveAsDishAsync(u.GetUserId(p)!, date, ct)))
            .WithName("SaveMealAsDish").Produces<DishDto>();

        g.MapGet("/dishes", async (ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            await s.ListDishesAsync(u.GetUserId(p)!, ct))
            .WithName("ListDishes").Produces<IReadOnlyList<DishDto>>();

        g.MapPost("/dishes", async (SaveDishRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            Results.Ok(await s.CreateDishAsync(u.GetUserId(p)!, r, ct)))
            .WithName("CreateDish").Produces<DishDto>();

        g.MapPut("/dishes/{id:guid}", async (Guid id, SaveDishRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateDishAsync(u.GetUserId(p)!, id, r, ct)))
            .WithName("UpdateDish").Produces<DishDto>();

        g.MapDelete("/dishes/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, MealPlanService s, CancellationToken ct) =>
        { await s.DeleteDishAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("DeleteDish").Produces(204);

        // ---- Shopping list ----
        g.MapGet("/shopping", async (ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            await s.ListAsync(u.GetUserId(p)!, ct))
            .WithName("ListShopping").Produces<IReadOnlyList<ShoppingItemDto>>();

        g.MapPost("/shopping", async (SaveShoppingItemRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            Results.Ok(await s.AddAsync(u.GetUserId(p)!, r, ct)))
            .WithName("AddShoppingItem").Produces<ShoppingItemDto>();

        g.MapPut("/shopping/{id:guid}", async (Guid id, SaveShoppingItemRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateAsync(u.GetUserId(p)!, id, r, ct)))
            .WithName("UpdateShoppingItem").Produces<ShoppingItemDto>();

        g.MapDelete("/shopping/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("DeleteShoppingItem").Produces(204);

        g.MapPut("/shopping/{id:guid}/bought", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
        { await s.SetBoughtAsync(u.GetUserId(p)!, id, true, ct); return Results.NoContent(); })
            .WithName("MarkShoppingBought").Produces(204);

        g.MapDelete("/shopping/{id:guid}/bought", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
        { await s.SetBoughtAsync(u.GetUserId(p)!, id, false, ct); return Results.NoContent(); })
            .WithName("UnmarkShoppingBought").Produces(204);

        g.MapDelete("/shopping/bought", async (ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            Results.Ok(await s.ClearBoughtAsync(u.GetUserId(p)!, ct)))
            .WithName("ClearBoughtShopping").Produces<int>();

        g.MapGet("/shopping/category-suggestion", (string? name) => ShoppingService.SuggestCategory(name))
            .WithName("SuggestShoppingCategory").Produces<CategorySuggestionDto>();

        g.MapGet("/shopping/favorites", async (ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            await s.ListFavoritesAsync(u.GetUserId(p)!, ct))
            .WithName("ListFavorites").Produces<IReadOnlyList<FavoriteDto>>();

        g.MapPost("/shopping/favorites", async (SaveShoppingItemRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            Results.Ok(await s.AddFavoriteAsync(u.GetUserId(p)!, r, ct)))
            .WithName("AddFavorite").Produces<FavoriteDto>();

        g.MapDelete("/shopping/favorites/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
        { await s.DeleteFavoriteAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("DeleteFavorite").Produces(204);

        // true when the item was put on the list, false when it was already there.
        g.MapPost("/shopping/favorites/{id:guid}/add", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, ShoppingService s, CancellationToken ct) =>
            Results.Ok(await s.AddFavoriteToListAsync(u.GetUserId(p)!, id, ct)))
            .WithName("AddFavoriteToList").Produces<bool>();

        // ---- Cleaning ----
        g.MapGet("/cleaning", async (ClaimsPrincipal p, UserManager<IdentityUser> u, CleaningService s, CancellationToken ct) =>
            await s.ListAsync(u.GetUserId(p)!, ct))
            .WithName("ListCleaning").Produces<IReadOnlyList<CleaningDto>>();

        g.MapPost("/cleaning", async (SaveCleaningRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, CleaningService s, CancellationToken ct) =>
            Results.Ok(await s.CreateAsync(u.GetUserId(p)!, r, ct)))
            .WithName("CreateCleaning").Produces<CleaningDto>();

        g.MapPut("/cleaning/{id:guid}", async (Guid id, SaveCleaningRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, CleaningService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateAsync(u.GetUserId(p)!, id, r, ct)))
            .WithName("UpdateCleaning").Produces<CleaningDto>();

        g.MapDelete("/cleaning/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, CleaningService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("DeleteCleaning").Produces(204);

        g.MapPut("/cleaning/{id:guid}/done", async (Guid id, MarkCleaningDoneRequest? r, ClaimsPrincipal p, UserManager<IdentityUser> u, CleaningService s, CancellationToken ct) =>
            Results.Ok(await s.MarkDoneAsync(u.GetUserId(p)!, id, r, ct)))
            .WithName("MarkCleaningDone").Produces<CleaningDto>();
    }
}
