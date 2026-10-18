using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace VoresFamilie.Web;

/// <summary>Sends the auth cookie on cross-origin calls (development only; same-origin in production).</summary>
public sealed class CookieHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
