using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VoresFamilie.Web;
using VoresFamilie.Web.Api;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In production (and behind the dev proxy / a dev tunnel) the page and the API share one origin, so cookies just work.
// Only when the page is opened on the separate Web dev server (Visual Studio F5) do we call the API on its own port.
// "ApiBaseUrls" in appsettings.Development.json lists those page addresses and the matching API addresses;
// http and https must match, because the auth cookie is SameSite=Lax.
var apiMap = builder.Configuration.GetSection("ApiBaseUrls").Get<List<ApiBaseUrl>>() ?? [];
var apiBase = apiMap.FirstOrDefault(m => string.Equals(m.From, builder.HostEnvironment.BaseAddress, StringComparison.OrdinalIgnoreCase))?.To;
var baseAddress = new Uri(string.IsNullOrEmpty(apiBase) ? builder.HostEnvironment.BaseAddress : apiBase);builder.Services.AddScoped(sp => new HttpClient(new CookieHandler { InnerHandler = new HttpClientHandler() }) { BaseAddress = baseAddress });
builder.Services.AddScoped<IApiClient>(sp => new ApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<Session>();
builder.Services.AddScoped<Emojis>();

await builder.Build().RunAsync();

/// <summary>One entry of ApiBaseUrls: the page address and the API address it should talk to.</summary>
public sealed record ApiBaseUrl(string From, string To);

