using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Api.Tests;

public class DailyFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);
    private static string D(DateOnly d) => d.ToString("yyyy-MM-dd");

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> RegisteredAsync(string email)
    {
        var c = NewClient();
        var r = await c.PostAsync("/api/auth/register", new { email, password = "hemmelig123" });
        Assert.True(r.StatusCode == HttpStatusCode.NoContent, await r.Content.ReadAsStringAsync());
        return c;
    }

    [Fact]
    public async Task Daily_endpoints_require_login()
    {
        var c = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/api/checklist/day?date={D(Today)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/todos")).StatusCode);
    }

    [Fact]
    public async Task Routines_tasks_and_todos_flow()
    {
        var admin = await RegisteredAsync("daily-admin@example.com");
        await admin.PostAsync("/api/families", new CreateFamilyRequest("Familien", "Mor"));
        var inv = await (await admin.PostAsync("/api/invitations", new CreateInvitationRequest(Role.Barn, null))).ReadAsync<CreatedInvitationDto>();
        var child = await RegisteredAsync("daily-emma@example.com");
        await child.PostAsync($"/api/invitations/{inv!.Token}/accept", new AcceptInvitationRequest("Emma"));
        var emma = (await (await admin.GetAsync("/api/members")).ReadAsync<List<MemberDto>>())!.Single(m => m.DisplayName == "Emma");

        // Admin builds the lists.
        var routine = await (await admin.PostAsync("/api/checklist-items",
            new SaveChecklistItemRequest(ChecklistKind.Routine, RoutinePart.Morning, emma.Id, "Børst tænder", (int)DaysOfWeek.All))).ReadAsync<ChecklistItemDto>();
        await admin.PostAsync("/api/checklist-items",
            new SaveChecklistItemRequest(ChecklistKind.HouseholdTask, null, null, "Rydde køkken", (int)DaysOfWeek.All));
        var tomorrowOnly = new SaveChecklistItemRequest(ChecklistKind.HouseholdTask, null, emma.Id, "Madpakker", (int)ToFlag(Today.AddDays(1).DayOfWeek));
        await admin.PostAsync("/api/checklist-items", tomorrowOnly);

        // Child cannot edit.
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PostAsync("/api/checklist-items", tomorrowOnly)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.DeleteAsync($"/api/checklist-items/{routine!.Id}")).StatusCode);

        // Today: the routine and the task for everyone; "Madpakker" is only due tomorrow.
        var day = await (await child.GetAsync($"/api/checklist/day?date={D(Today)}")).ReadAsync<ChecklistDayDto>();
        Assert.Equal(["Børst tænder", "Rydde køkken"], day!.Entries.Select(e => e.Title));
        Assert.All(day.Entries, e => Assert.True(e.CanCheck && !e.Done));

        Assert.Equal(HttpStatusCode.NoContent, (await child.PutAsync($"/api/checklist/{routine.Id}/done?date={D(Today)}", new { })).StatusCode);
        day = await (await child.GetAsync($"/api/checklist/day?date={D(Today)}")).ReadAsync<ChecklistDayDto>();
        Assert.True(day!.Entries.Single(e => e.Title == "Børst tænder").Done);

        // The next day starts clean, and old days cannot be changed.
        var tomorrow = await (await child.GetAsync($"/api/checklist/day?date={D(Today.AddDays(1))}")).ReadAsync<ChecklistDayDto>();
        Assert.Equal(["Børst tænder", "Rydde køkken", "Madpakker"], tomorrow!.Entries.Select(e => e.Title).OrderBy(t => t == "Madpakker").ThenBy(t => t == "Rydde køkken"));
        Assert.All(tomorrow.Entries, e => Assert.False(e.Done));
        Assert.Equal(HttpStatusCode.BadRequest, (await child.PutAsync($"/api/checklist/{routine.Id}/done?date={D(Today.AddDays(-3))}", new { })).StatusCode);

        // Untick, then admin ticks for the child.
        Assert.Equal(HttpStatusCode.NoContent, (await child.DeleteAsync($"/api/checklist/{routine.Id}/done?date={D(Today)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/checklist/{routine.Id}/done?date={D(Today)}", new { })).StatusCode);
        var asAdmin = await (await admin.GetAsync($"/api/checklist/day?date={D(Today)}&memberId={emma.Id}")).ReadAsync<ChecklistDayDto>();
        Assert.Equal("Mor", asAdmin!.Entries.Single(e => e.Title == "Børst tænder").CompletedByName);

        // Reorder and delete.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/checklist-items/{routine.Id}/move", new MoveChecklistItemRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/checklist-items/{routine.Id}")).StatusCode);

        // ToDo: assigned to the child, who may tick it but not create.
        var todo = await (await admin.PostAsync("/api/todos", new SaveTodoRequest("Tømme opvasker", null, emma.Id, Today.AddDays(1)))).ReadAsync<TodoDto>();
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PostAsync("/api/todos", new SaveTodoRequest("x", null, null, null))).StatusCode);
        var mine = await (await child.GetAsync("/api/todos?filter=Mine")).ReadAsync<List<TodoDto>>();
        Assert.Equal(todo!.Id, mine!.Single().Id);
        Assert.True(mine!.Single().CanCheck);
        Assert.Equal(HttpStatusCode.NoContent, (await child.PutAsync($"/api/todos/{todo.Id}/done", new { })).StatusCode);
        Assert.Empty((await (await admin.GetAsync("/api/todos?filter=Open")).ReadAsync<List<TodoDto>>())!);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.DeleteAsync("/api/todos/completed")).StatusCode);
        Assert.Equal(1, await (await admin.DeleteAsync("/api/todos/completed")).ReadAsync<int>());
        Assert.Empty((await (await admin.GetAsync("/api/todos")).ReadAsync<List<TodoDto>>())!);
    }

    private static DaysOfWeek ToFlag(DayOfWeek d) => DaysOfWeekExtensions.ToFlag(d);
}

