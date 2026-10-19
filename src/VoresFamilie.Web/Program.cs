using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VoresFamilie.Web;
using VoresFamilie.Web.Api;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same origin as the API (reverse proxy routes /api to the backend); cookies are sent automatically.
// In production the API is on the same origin (reverse proxy). In development the Web dev server is separate, so ApiBaseUrl points at the API.
// The auth cookie is SameSite=Lax and http/https count as different sites, so the API must use the same scheme as the page.
var isHttps = builder.HostEnvironment.BaseAddress.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
var apiBase = isHttps ? builder.Configuration["ApiBaseUrlHttps"] ?? builder.Configuration["ApiBaseUrl"] : builder.Configuration["ApiBaseUrl"];
var baseAddress = new Uri(string.IsNullOrEmpty(apiBase) ? builder.HostEnvironment.BaseAddress : apiBase);
builder.Services.AddScoped(sp => new HttpClient(new CookieHandler { InnerHandler = new HttpClientHandler() }) { BaseAddress = baseAddress });
builder.Services.AddScoped<IApiClient>(sp => new ApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<Session>();

await builder.Build().RunAsync();


