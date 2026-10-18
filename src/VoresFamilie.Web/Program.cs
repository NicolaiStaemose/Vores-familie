using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VoresFamilie.Web;
using VoresFamilie.Web.Api;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same origin as the API (reverse proxy routes /api to the backend); cookies are sent automatically.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IApiClient>(sp => new ApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<Session>();

await builder.Build().RunAsync();
