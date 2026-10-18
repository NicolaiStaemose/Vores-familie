using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using VoresFamilie.Application;

namespace VoresFamilie.Api;

public static class FamilyEndpoints
{
    public static void MapFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").RequireAuthorization().WithTags("Family");

        g.MapPost("/families", async (CreateFamilyRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
            Results.Ok(await s.CreateAsync(u.GetUserId(p)!, r, ct)))
            .WithName("CreateFamily").Produces<FamilyDto>();

        g.MapGet("/families/current", async (ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
            await s.GetCurrentAsync(u.GetUserId(p)!, ct) is { } f ? Results.Ok(f) : Results.NotFound())
            .WithName("GetCurrentFamily").Produces<FamilyDto>().Produces(404);

        g.MapGet("/members", async (ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
            await s.ListMembersAsync(u.GetUserId(p)!, ct))
            .WithName("ListMembers").Produces<IReadOnlyList<MemberDto>>();

        g.MapPut("/members/{id:guid}/role", async (Guid id, ChangeRoleRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
        { await s.ChangeRoleAsync(u.GetUserId(p)!, id, r, ct); return Results.NoContent(); })
            .WithName("ChangeRole").Produces(204);

        g.MapPut("/members/{id:guid}/permissions", async (Guid id, SetPermissionRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
        { await s.SetPermissionAsync(u.GetUserId(p)!, id, r, ct); return Results.NoContent(); })
            .WithName("SetPermission").Produces(204);

        g.MapPut("/members/{id:guid}/can-check-own-items", async (Guid id, SetCanCheckOwnItemsRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
        { await s.SetCanCheckOwnItemsAsync(u.GetUserId(p)!, id, r, ct); return Results.NoContent(); })
            .WithName("SetCanCheckOwnItems").Produces(204);

        g.MapDelete("/members/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
        { await s.RemoveMemberAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("RemoveMember").Produces(204);

        g.MapGet("/invitations", async (ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
            await s.ListInvitationsAsync(u.GetUserId(p)!, ct))
            .WithName("ListInvitations").Produces<IReadOnlyList<InvitationDto>>();

        g.MapPost("/invitations", async (CreateInvitationRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
            Results.Ok(await s.InviteAsync(u.GetUserId(p)!, r, ct)))
            .WithName("CreateInvitation").Produces<CreatedInvitationDto>();

        g.MapDelete("/invitations/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
        { await s.RevokeInvitationAsync(u.GetUserId(p)!, id, ct); return Results.NoContent(); })
            .WithName("RevokeInvitation").Produces(204);

        g.MapPost("/invitations/{token}/accept", async (string token, AcceptInvitationRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, FamilyService s, CancellationToken ct) =>
            Results.Ok(await s.AcceptInvitationAsync(u.GetUserId(p)!, token, r, ct)))
            .WithName("AcceptInvitation").Produces<FamilyDto>();
    }
}
