using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using VoresFamilie.Application;

namespace VoresFamilie.Api;

public static class CalendarEndpoints
{
    private const string StateCookie = "vf.gstate";

    public static void MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").RequireAuthorization().WithTags("Calendar");

        // ---- Google connection ----
        g.MapGet("/google/status", async (ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            await s.GetGoogleStatusAsync(u.GetUserId(p)!, ct))
            .WithName("GetGoogleStatus").Produces<GoogleStatusDto>();

        // Browser navigation (not called via the generated client): sends the user to Google's consent screen.
        g.MapGet("/google/connect", (string? returnUrl, HttpContext ctx, IGoogleAuthService google, IDataProtectionProvider dp, IConfiguration config) =>
        {
            if (!google.IsConfigured) return Results.Redirect(WithQuery(SafeReturn(returnUrl, config, ctx), "google", "ikke-sat-op"));
            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var safe = SafeReturn(returnUrl, config, ctx);
            ctx.Response.Cookies.Append(StateCookie, dp.CreateProtector("google-state").Protect($"{state}|{safe}"),
                new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10), IsEssential = true });
            return Results.Redirect(google.BuildAuthorizationUrl(state, RedirectUri(ctx)));
        }).ExcludeFromDescription();

        g.MapGet("/google/callback", async (string? code, string? state, string? error, HttpContext ctx, ClaimsPrincipal p,
            UserManager<IdentityUser> u, IGoogleAuthService google, IDataProtectionProvider dp, IConfiguration config, CancellationToken ct) =>
        {
            var fallback = SafeReturn(null, config, ctx);
            var cookie = ctx.Request.Cookies[StateCookie];
            ctx.Response.Cookies.Delete(StateCookie);
            string? returnUrl = null;
            try
            {
                var parts = cookie is null ? null : dp.CreateProtector("google-state").Unprotect(cookie).Split('|', 2);
                if (parts is { Length: 2 } && parts[0] == state) returnUrl = parts[1];
            }
            catch (CryptographicException) { /* tampered or expired cookie */ }
            if (returnUrl is null) return Results.Redirect(WithQuery(fallback, "google", "fejl"));
            if (error is not null || string.IsNullOrEmpty(code)) return Results.Redirect(WithQuery(returnUrl, "google", "afbrudt"));

            try { await google.ConnectAsync(u.GetUserId(p)!, code, RedirectUri(ctx), ct); }
            catch (Exception ex) when (ex is Domain.DomainException or Google.Apis.Auth.OAuth2.Responses.TokenResponseException)
            {
                return Results.Redirect(WithQuery(returnUrl, "google", "fejl"));
            }
            return Results.Redirect(WithQuery(returnUrl, "google", "forbundet"));
        }).ExcludeFromDescription();

        g.MapDelete("/google/connection", async (ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
        { await s.DisconnectGoogleAsync(u.GetUserId(p)!, ct); return Results.NoContent(); })
            .WithName("DisconnectGoogle").Produces(204);

        g.MapGet("/google/calendars", async (ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            await s.ListGoogleCalendarsAsync(u.GetUserId(p)!, ct))
            .WithName("ListGoogleCalendars").Produces<IReadOnlyList<GoogleCalendarDto>>();

        // ---- Calendars ----
        g.MapGet("/calendars", async (ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            await s.ListCalendarsAsync(u.GetUserId(p)!, ct))
            .WithName("ListCalendars").Produces<IReadOnlyList<CalendarDto>>();

        g.MapPost("/calendars", async (AddCalendarRequest r, ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            Results.Ok(await s.AddCalendarAsync(u.GetUserId(p)!, r, ct)))
            .WithName("AddCalendar").Produces<CalendarDto>();

        g.MapDelete("/calendars/{calendarId:guid}", async (Guid calendarId, ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
        { await s.RemoveCalendarAsync(u.GetUserId(p)!, calendarId, ct); return Results.NoContent(); })
            .WithName("RemoveCalendar").Produces(204);

        g.MapPost("/calendars/sync", async (ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            Results.Ok(await s.SyncNowAsync(u.GetUserId(p)!, ct)))
            .WithName("SyncCalendars").Produces<SyncResultDto>();

        // ---- Events ----
        g.MapGet("/events", async (DateTimeOffset from, DateTimeOffset to, Guid[]? calendarIds, ClaimsPrincipal p,
            UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            await s.GetEventsAsync(u.GetUserId(p)!, from, to, calendarIds is { Length: > 0 } ? calendarIds : null, ct))
            .WithName("ListEvents").Produces<IReadOnlyList<EventDto>>();

        g.MapPost("/calendars/{calendarId:guid}/events", async (Guid calendarId, SaveEventRequest r, ClaimsPrincipal p,
            UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            Results.Ok(await s.CreateEventAsync(u.GetUserId(p)!, calendarId, r, ct)))
            .WithName("CreateEvent").Produces<EventDto>();

        g.MapPut("/calendars/{calendarId:guid}/events/{eventId}", async (Guid calendarId, string eventId, SaveEventRequest r,
            ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateEventAsync(u.GetUserId(p)!, calendarId, eventId, r, ct)))
            .WithName("UpdateEvent").Produces<EventDto>();

        g.MapDelete("/calendars/{calendarId:guid}/events/{eventId}", async (Guid calendarId, string eventId,
            ClaimsPrincipal p, UserManager<IdentityUser> u, CalendarService s, CancellationToken ct) =>
        { await s.DeleteEventAsync(u.GetUserId(p)!, calendarId, eventId, ct); return Results.NoContent(); })
            .WithName("DeleteEvent").Produces(204);

        // ---- Homework ----
        g.MapGet("/homework", async (DateTimeOffset from, DateTimeOffset to, Guid? memberId, ClaimsPrincipal p,
            UserManager<IdentityUser> u, HomeworkService s, CancellationToken ct) =>
            await s.ListAsync(u.GetUserId(p)!, from, to, memberId, ct))
            .WithName("ListHomework").Produces<IReadOnlyList<HomeworkItemDto>>();

        g.MapPut("/homework/{calendarId:guid}/{eventId}/done", async (Guid calendarId, string eventId, ClaimsPrincipal p,
            UserManager<IdentityUser> u, HomeworkService s, CancellationToken ct) =>
        { await s.SetDoneAsync(u.GetUserId(p)!, calendarId, eventId, true, ct); return Results.NoContent(); })
            .WithName("MarkHomeworkDone").Produces(204);

        g.MapDelete("/homework/{calendarId:guid}/{eventId}/done", async (Guid calendarId, string eventId, ClaimsPrincipal p,
            UserManager<IdentityUser> u, HomeworkService s, CancellationToken ct) =>
        { await s.SetDoneAsync(u.GetUserId(p)!, calendarId, eventId, false, ct); return Results.NoContent(); })
            .WithName("UnmarkHomeworkDone").Produces(204);
    }

    /// <summary>Google redirects back to the API itself, so this exact URL must be registered in the Google OAuth client.</summary>
    private static string RedirectUri(HttpContext ctx) => $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/google/callback";

    /// <summary>Only same-site relative paths or the configured web origins (dev) are allowed as return targets.</summary>
    private static string SafeReturn(string? returnUrl, IConfiguration config, HttpContext ctx)
    {
        if (string.IsNullOrEmpty(returnUrl)) return "/";
        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")) return returnUrl;
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            var origin = uri.GetLeftPart(UriPartial.Authority);
            var allowed = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
            if (allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return returnUrl;
        }
        return "/";
    }

    private static string WithQuery(string url, string key, string value) =>
        url + (url.Contains('?') ? '&' : '?') + $"{key}={Uri.EscapeDataString(value)}";
}
