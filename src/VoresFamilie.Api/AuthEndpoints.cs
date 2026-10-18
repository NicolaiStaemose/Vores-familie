using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using VoresFamilie.Application;

namespace VoresFamilie.Api;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record MeDto(string UserId, string Email, FamilyDto? Family);
public sealed record AuthProvidersDto(bool Google);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app, bool googleEnabled)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapGet("/providers", () => new AuthProvidersDto(googleEnabled)).WithName("GetAuthProviders");

        g.MapPost("/register", async (RegisterRequest r, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) =>
        {
            var user = new IdentityUser { UserName = r.Email.Trim(), Email = r.Email.Trim() };
            var result = await users.CreateAsync(user, r.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new ErrorDto(string.Join(" ", result.Errors.Select(e => e.Description))));
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.NoContent();
        }).WithName("Register").Produces(204).Produces<ErrorDto>(400);

        g.MapPost("/login", async (LoginRequest r, SignInManager<IdentityUser> signIn) =>
        {
            var result = await signIn.PasswordSignInAsync(r.Email.Trim(), r.Password, isPersistent: true, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Json(new ErrorDto("Forkert e-mail eller adgangskode."), statusCode: 401);
        }).WithName("Login").Produces(204).Produces<ErrorDto>(401);

        g.MapPost("/logout", async (SignInManager<IdentityUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).WithName("Logout").Produces(204);

        // Browser navigation (not called via the generated client).
        g.MapGet("/google", (string? returnUrl, SignInManager<IdentityUser> signIn) =>
        {
            if (!googleEnabled) return Results.NotFound();
            var props = signIn.ConfigureExternalAuthenticationProperties("Google", $"/api/auth/google/callback?returnUrl={Uri.EscapeDataString(Safe(returnUrl))}");
            return Results.Challenge(props, ["Google"]);
        }).ExcludeFromDescription();

        g.MapGet("/google/callback", async (string? returnUrl, SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users) =>
        {
            var info = await signIn.GetExternalLoginInfoAsync();
            if (info is null) return Results.Redirect("/?loginfejl=1");

            var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
            if (!result.Succeeded)
            {
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(email)) return Results.Redirect("/?loginfejl=1");
                var user = await users.FindByEmailAsync(email)
                    ?? new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                if (user.Id is null || await users.FindByIdAsync(user.Id) is null)
                {
                    if (!(await users.CreateAsync(user)).Succeeded) return Results.Redirect("/?loginfejl=1");
                }
                if (!(await users.AddLoginAsync(user, info)).Succeeded) return Results.Redirect("/?loginfejl=1");
                await signIn.SignInAsync(user, isPersistent: true);
            }
            await signIn.Context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect(Safe(returnUrl));
        }).ExcludeFromDescription();

        app.MapGet("/api/me", async (ClaimsPrincipal principal, UserManager<IdentityUser> users, FamilyService families, CancellationToken ct) =>
        {
            var userId = users.GetUserId(principal)!;
            var user = await users.FindByIdAsync(userId);
            return new MeDto(userId, user?.Email ?? "", await families.GetCurrentAsync(userId, ct));
        }).RequireAuthorization().WithName("GetMe").WithTags("Auth").Produces<MeDto>();
    }

    private static string Safe(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";
}

