using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using VoresFamilie.Application;

namespace VoresFamilie.Api;

public static class PushEndpoints
{
    public static void MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/push").RequireAuthorization().WithTags("Push");

        // The public VAPID key the browser needs to subscribe. Null when the server has no keys.
        g.MapGet("/public-key", (PushService s) => s.GetPublicKey())
            .WithName("GetPushPublicKey").Produces<PublicKeyDto>();

        g.MapGet("/settings", async (ClaimsPrincipal p, UserManager<IdentityUser> u, PushService s, CancellationToken ct) =>
            await s.GetSettingsAsync(u.GetUserId(p)!, ct))
            .WithName("GetPushSettings").Produces<PushSettingsDto>();

        g.MapPut("/settings", async (SavePushSettingsRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, PushService s, CancellationToken ct) =>
            Results.Ok(await s.SaveSettingsAsync(u.GetUserId(p)!, r, ct)))
            .WithName("SavePushSettings").Produces<PushSettingsDto>();

        g.MapPost("/subscriptions", async (SubscribePushRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, PushService s, CancellationToken ct) =>
        { await s.SubscribeAsync(u.GetUserId(p)!, r, ct); return Results.NoContent(); })
            .WithName("SubscribePush").Produces(204);

        g.MapPost("/subscriptions/remove", async (UnsubscribePushRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, PushService s, CancellationToken ct) =>
        { await s.UnsubscribeAsync(u.GetUserId(p)!, r, ct); return Results.NoContent(); })
            .WithName("UnsubscribePush").Produces(204);

        g.MapPost("/test", async (ClaimsPrincipal p, UserManager<IdentityUser> u, PushService s, CancellationToken ct) =>
            Results.Ok(await s.SendTestAsync(u.GetUserId(p)!, ct)))
            .WithName("SendTestPush").Produces<TestPushResultDto>();
    }
}
