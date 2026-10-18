using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using VoresFamilie.Application;

namespace VoresFamilie.Api.Tests;

public class FamilyFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> RegisteredAsync(string email)
    {
        var c = NewClient();
        var r = await c.PostAsync("/api/auth/register", new { email, password = "hemmelig123" });
        Assert.True(r.StatusCode == HttpStatusCode.NoContent, await r.Content.ReadAsStringAsync());
        return c;
    }

    [Fact]
    public async Task Health_is_ok() =>
        Assert.Equal(HttpStatusCode.OK, (await NewClient().GetAsync("/healthz")).StatusCode);

    [Fact]
    public async Task Unauthenticated_requests_get_401()
    {
        var c = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/members")).StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_401()
    {
        await RegisteredAsync("a@example.com");
        var r = await NewClient().PostAsync("/api/auth/login", new { email = "a@example.com", password = "forkert-kode" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var r = await NewClient().PostAsync("/api/auth/register", new { email = "b@example.com", password = "kort" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Full_flow_register_create_invite_accept_and_change_permission()
    {
        var admin = await RegisteredAsync("admin@example.com");
        var me = await (await admin.GetAsync("/api/me")).ReadAsync<MeDtoShape>();
        Assert.Null(me!.Family);

        var created = await admin.PostAsync("/api/families", new CreateFamilyRequest("Familien", "Nicolai"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var inv = await (await admin.PostAsync("/api/invitations", new CreateInvitationRequest(Domain.Role.Barn, null)))
            .ReadAsync<CreatedInvitationDto>();

        var child = await RegisteredAsync("emma@example.com");
        var accepted = await child.PostAsync($"/api/invitations/{inv!.Token}/accept", new AcceptInvitationRequest("Emma"));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // Same token cannot be reused.
        var other = await RegisteredAsync("noah@example.com");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await other.PostAsync($"/api/invitations/{inv.Token}/accept", new AcceptInvitationRequest("Noah"))).StatusCode);

        var members = await (await admin.GetAsync("/api/members")).ReadAsync<List<MemberDto>>();
        Assert.Equal(2, members!.Count);
        var emma = members.Single(m => m.DisplayName == "Emma");
        Assert.Equal(Domain.AccessLevel.View, emma.Permissions[Domain.Area.Shopping]);

        // Child can view members but not manage.
        Assert.Equal(HttpStatusCode.OK, (await child.GetAsync("/api/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.GetAsync("/api/invitations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await child.PutAsync($"/api/members/{emma.Id}/permissions", new SetPermissionRequest(Domain.Area.Shopping, Domain.AccessLevel.Edit))).StatusCode);

        // Admin changes permission; it sticks.
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PutAsync($"/api/members/{emma.Id}/permissions", new SetPermissionRequest(Domain.Area.Shopping, Domain.AccessLevel.Edit))).StatusCode);
        var after = await (await admin.GetAsync("/api/members")).ReadAsync<List<MemberDto>>();
        Assert.Equal(Domain.AccessLevel.Edit, after!.Single(m => m.DisplayName == "Emma").Permissions[Domain.Area.Shopping]);

        // Last admin cannot be removed.
        var adminMember = after!.Single(m => m.DisplayName == "Nicolai");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/members/{adminMember.Id}")).StatusCode);
    }

    private sealed record MeDtoShape(string UserId, string Email, FamilyDto? Family);
}


