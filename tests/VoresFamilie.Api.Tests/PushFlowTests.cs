using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using VoresFamilie.Application;

namespace VoresFamilie.Api.Tests;

public class PushFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> FamilyMemberAsync(string email)
    {
        var c = NewClient();
        var r = await c.PostAsync("/api/auth/register", new { email, password = "hemmelig123" });
        Assert.True(r.StatusCode == HttpStatusCode.NoContent, await r.Content.ReadAsStringAsync());
        await c.PostAsync("/api/families", new CreateFamilyRequest("Familien", "Mor"));
        return c;
    }

    [Fact]
    public async Task Push_endpoints_require_login()
    {
        var c = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/push/public-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/push/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync("/api/push/test", new { })).StatusCode);
    }

    [Fact]
    public async Task Subscribe_change_settings_and_unsubscribe()
    {
        var c = await FamilyMemberAsync("push-mor@example.com");

        var key = await (await c.GetAsync("/api/push/public-key")).ReadAsync<PublicKeyDto>();
        Assert.False(string.IsNullOrEmpty(key!.Key));

        var before = await (await c.GetAsync("/api/push/settings")).ReadAsync<PushSettingsDto>();
        Assert.Equal(new PushSettingsDto(true, true, true, 0), before);

        var device = new SubscribePushRequest("https://push.example.com/device-1", "p256dh-key", "auth-secret");
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/push/subscriptions", device)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/push/subscriptions", device)).StatusCode); // same device again
        Assert.Equal(1, (await (await c.GetAsync("/api/push/settings")).ReadAsync<PushSettingsDto>())!.Devices);

        var saved = await (await c.PutAsync("/api/push/settings", new SavePushSettingsRequest(false, true))).ReadAsync<PushSettingsDto>();
        Assert.Equal(new PushSettingsDto(true, false, true, 1), saved);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsync("/api/push/subscriptions", new SubscribePushRequest("http://insecure.example.com/x", "k", "a"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/push/subscriptions/remove", new UnsubscribePushRequest(device.Endpoint))).StatusCode);
        Assert.Equal(0, (await (await c.GetAsync("/api/push/settings")).ReadAsync<PushSettingsDto>())!.Devices);
    }

    [Fact]
    public async Task Test_message_needs_a_registered_device()
    {
        var c = await FamilyMemberAsync("push-far@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/push/test", new { })).StatusCode);
    }
}
