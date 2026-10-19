using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using VoresFamilie.Api;
using VoresFamilie.Application;
using VoresFamilie.Domain;
using VoresFamilie.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Enums are serialized as strings; say so in the contract so generated clients use string enums.
builder.Services.AddOpenApi(o => o.AddSchemaTransformer((schema, context, _) =>
{
    if (context.JsonTypeInfo.Type.IsEnum) schema.Type = Microsoft.OpenApi.JsonSchemaType.String;
    return Task.CompletedTask;
}));
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<VoresFamilie.Infrastructure.AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "vf.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
    // API: answer 401/403 instead of redirecting to a login page.
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleEnabled = !string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret);
if (googleEnabled)
{
    builder.Services.AddAuthentication().AddGoogle(o =>
    {
        o.ClientId = googleId!;
        o.ClientSecret = googleSecret!;
        o.CallbackPath = "/api/auth/google-signin";
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddAuthorization();

// Only needed when the Web dev server runs on its own port (Visual Studio F5). In production everything is same-origin behind the proxy.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors();
app.UseExceptionMapping();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi("/api/openapi.json");
if (app.Environment.IsDevelopment())
    app.MapScalarApiReference("/scalar", o => o.WithOpenApiRoutePattern("/api/openapi.json").WithTitle("Vores familie API"));
app.MapGet("/healthz", () => Results.Ok("ok")).ExcludeFromDescription();
app.MapAuthEndpoints(googleEnabled);
app.MapFamilyEndpoints();

// Skip DB setup when the OpenAPI build task starts the app only to read the document.
if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
    await app.Services.InitializeDatabaseAsync();

app.Run();

public partial class Program;



