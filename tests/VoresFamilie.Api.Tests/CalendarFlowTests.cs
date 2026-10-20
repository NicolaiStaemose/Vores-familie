using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VoresFamilie.Application;
using VoresFamilie.Domain;
using VoresFamilie.Infrastructure;

namespace VoresFamilie.Api.Tests;

public class CalendarFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task Calendar_endpoints_require_login()
    {
        var c = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/calendars")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/homework?from=2026-01-01&to=2026-02-01")).StatusCode);
    }

    [Fact]
    public async Task Connect_flow_redirects_back_with_status()
    {
        var admin = await RegisteredAsync("connect@example.com");
        var start = await admin.GetAsync("/api/google/connect?returnUrl=/kalender");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var location = start.Headers.Location!;
        Assert.Contains("/api/google/callback", location.ToString());

        var back = await admin.GetAsync(location.PathAndQuery); // the fake Google "consent screen" redirects straight back
        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.Equal("/kalender?google=forbundet", back.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Callback_with_wrong_state_is_rejected()
    {
        var admin = await RegisteredAsync("state@example.com");
        await admin.GetAsync("/api/google/connect");
        var back = await admin.GetAsync("/api/google/callback?code=fake&state=forkert");
        Assert.Contains("google=fejl", back.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Connect_does_not_redirect_to_foreign_sites()
    {
        var admin = await RegisteredAsync("open@example.com");
        await admin.GetAsync("/api/google/connect?returnUrl=https://evil.example/steal");
        var cookieless = await admin.GetAsync("/api/google/callback?code=fake&state=x");
        Assert.StartsWith("/", cookieless.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Full_flow_calendars_events_and_homework()
    {
        var admin = await RegisteredAsync("cal-admin@example.com");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/families", new CreateFamilyRequest("Familien", "Nicolai"))).StatusCode);
        var inv = await (await admin.PostAsync("/api/invitations", new CreateInvitationRequest(Role.Barn, null))).ReadAsync<CreatedInvitationDto>();
        var child = await RegisteredAsync("cal-emma@example.com");
        Assert.Equal(HttpStatusCode.OK, (await child.PostAsync($"/api/invitations/{inv!.Token}/accept", new AcceptInvitationRequest("Emma"))).StatusCode);
        var members = await (await admin.GetAsync("/api/members")).ReadAsync<List<MemberDto>>();
        var emma = members!.Single(m => m.DisplayName == "Emma");

        var status = await (await admin.GetAsync("/api/google/status")).ReadAsync<GoogleStatusDto>();
        Assert.True(status!.Configured && status.Connected);
        var google = await (await admin.GetAsync("/api/google/calendars")).ReadAsync<List<GoogleCalendarDto>>();
        Assert.Contains(google!, g => g.Id == "demo-emma-homework");

        // Only admin may add calendars.
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PostAsync("/api/calendars",
            new AddCalendarRequest("demo-family", "Familien", CalendarKind.Family, null, null))).StatusCode);

        var family = await (await admin.PostAsync("/api/calendars",
            new AddCalendarRequest("demo-family", "Familien", CalendarKind.Family, null, null))).ReadAsync<CalendarDto>();
        var homework = await (await admin.PostAsync("/api/calendars",
            new AddCalendarRequest("demo-emma-homework", "Emma lektier", CalendarKind.Homework, emma.Id, null))).ReadAsync<CalendarDto>();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/calendars",
            new AddCalendarRequest("demo-family", "Igen", CalendarKind.Family, null, null))).StatusCode);

        var visible = await (await child.GetAsync("/api/calendars")).ReadAsync<List<CalendarDto>>();
        Assert.Equal(2, visible!.Count);
        Assert.False(visible.Single(c => c.Id == family!.Id).CanEdit);

        // Events in a wide window.
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(14).ToString("O"));
        var events = await (await child.GetAsync($"/api/events?from={from}&to={to}")).ReadAsync<List<EventDto>>();
        Assert.Contains(events!, e => e.Title == "Svømning");
        Assert.Contains(events!, e => e.Title == "99 arter" && !e.IsHomework);

        // Homework list and ticking.
        var items = await (await child.GetAsync($"/api/homework?from={from}&to={to}")).ReadAsync<List<HomeworkItemDto>>();
        Assert.NotEmpty(items!);
        Assert.All(items!, i => { Assert.Equal("Emma", i.ChildName); Assert.Equal("Dansk", i.Subject); Assert.True(i.CanCheck); });
        var first = items![0];
        Assert.Equal(HttpStatusCode.NoContent, (await child.PutAsync($"/api/homework/{first.CalendarId}/{first.EventId}/done", new { })).StatusCode);
        var after = await (await child.GetAsync($"/api/homework?from={from}&to={to}")).ReadAsync<List<HomeworkItemDto>>();
        Assert.True(after!.Single(i => i.EventId == first.EventId).Done);
        Assert.Equal(HttpStatusCode.NoContent, (await child.DeleteAsync($"/api/homework/{first.CalendarId}/{first.EventId}/done")).StatusCode);

        // Child cannot create in the family calendar; admin can, and it reaches "Google".
        var body = new SaveEventRequest("Forældremøde", null, null, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddDays(3).AddHours(1), false);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PostAsync($"/api/calendars/{family!.Id}/events", body)).StatusCode);
        var fake = factory.Services.GetRequiredService<FakeGoogleCalendarGateway>();
        var before = fake.EventCount("demo-family");
        var created = await (await admin.PostAsync($"/api/calendars/{family.Id}/events", body)).ReadAsync<EventDto>();
        Assert.Equal(before + 1, fake.EventCount("demo-family"));

        var updated = await admin.PutAsync($"/api/calendars/{family.Id}/events/{created!.Id}", body with { Title = "Forældremøde 2" });
        Assert.Equal("Forældremøde 2", (await updated.ReadAsync<EventDto>())!.Title);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/calendars/{family.Id}/events/{created.Id}")).StatusCode);
        Assert.Equal(before, fake.EventCount("demo-family"));

        // Removing a calendar.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/calendars/{homework!.Id}")).StatusCode);
        Assert.Single((await (await child.GetAsync("/api/calendars")).ReadAsync<List<CalendarDto>>())!);
    }
}
