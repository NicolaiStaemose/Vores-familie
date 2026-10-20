using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using VoresFamilie.Application;

namespace VoresFamilie.Api;

public static class DailyEndpoints
{
    public static void MapDailyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").RequireAuthorization().WithTags("Daily");

        // ---- Routines and household tasks: definitions ----
        g.MapGet("/checklist-items", async (ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
            await s.ListItemsAsync(u.GetUserId(p)!, ct))
            .WithName("ListChecklistItems").Produces<IReadOnlyList<ChecklistItemDto>>();

        g.MapGet("/checklist-items/icon-suggestion", (string? title) => ChecklistService.SuggestIcon(title))
            .WithName("SuggestChecklistIcon").Produces<IconSuggestionDto>();

        g.MapPost("/checklist-items", async (SaveChecklistItemRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
            Results.Ok(await s.CreateItemAsync(u.GetUserId(p)!, r, ct)))
            .WithName("CreateChecklistItem").Produces<ChecklistItemDto>();

        g.MapPut("/checklist-items/{id:guid}", async (Guid id, SaveChecklistItemRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateItemAsync(u.GetUserId(p)!, id, r, ct)))
            .WithName("UpdateChecklistItem").Produces<ChecklistItemDto>();

        g.MapDelete("/checklist-items/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
        { await s.DeleteItemAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("DeleteChecklistItem").Produces(204);

        g.MapPut("/checklist-items/{id:guid}/move", async (Guid id, MoveChecklistItemRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
        { await s.MoveItemAsync(u.GetUserId(p)!, id, r.Up, ct); return Results.NoContent(); })
            .WithName("MoveChecklistItem").Produces(204);

        // ---- The day ----
        g.MapGet("/checklist/day", async (DateOnly date, Guid? memberId, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
            await s.GetDayAsync(u.GetUserId(p)!, date, memberId, ct))
            .WithName("GetChecklistDay").Produces<ChecklistDayDto>();

        g.MapPut("/checklist/{itemId:guid}/done", async (Guid itemId, DateOnly date, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
        { await s.SetDoneAsync(u.GetUserId(p)!, itemId, date, true, ct); return Results.NoContent(); })
            .WithName("MarkChecklistDone").Produces(204);

        g.MapDelete("/checklist/{itemId:guid}/done", async (Guid itemId, DateOnly date, ClaimsPrincipal p, UserManager<IdentityUser> u, ChecklistService s, CancellationToken ct) =>
        { await s.SetDoneAsync(u.GetUserId(p)!, itemId, date, false, ct); return Results.NoContent(); })
            .WithName("UnmarkChecklistDone").Produces(204);

        // ---- ToDo ----
        g.MapGet("/todos", async (TodoFilter? filter, ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
            await s.ListAsync(u.GetUserId(p)!, filter ?? TodoFilter.All, ct))
            .WithName("ListTodos").Produces<IReadOnlyList<TodoDto>>();

        g.MapPost("/todos", async (SaveTodoRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
            Results.Ok(await s.CreateAsync(u.GetUserId(p)!, r, ct)))
            .WithName("CreateTodo").Produces<TodoDto>();

        g.MapPut("/todos/{id:guid}", async (Guid id, SaveTodoRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateAsync(u.GetUserId(p)!, id, r, ct)))
            .WithName("UpdateTodo").Produces<TodoDto>();

        g.MapDelete("/todos/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("DeleteTodo").Produces(204);

        g.MapPut("/todos/{id:guid}/done", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
        { await s.SetDoneAsync(u.GetUserId(p)!, id, true, ct); return Results.NoContent(); })
            .WithName("MarkTodoDone").Produces(204);

        g.MapDelete("/todos/{id:guid}/done", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
        { await s.SetDoneAsync(u.GetUserId(p)!, id, false, ct); return Results.NoContent(); })
            .WithName("UnmarkTodoDone").Produces(204);

        g.MapDelete("/todos/completed", async (ClaimsPrincipal p, UserManager<IdentityUser> u, TodoService s, CancellationToken ct) =>
            Results.Ok(await s.ClearCompletedAsync(u.GetUserId(p)!, ct)))
            .WithName("ClearCompletedTodos").Produces<int>();
    }
}

