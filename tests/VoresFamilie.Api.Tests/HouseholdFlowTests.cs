using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Api.Tests;

public class HouseholdFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task Household_endpoints_require_login()
    {
        var c = NewClient();
        foreach (var url in new[] { "/api/shopping", "/api/shopping/habits", "/api/cleaning", "/api/dishes", $"/api/meals?from={D(Today)}&to={D(Today)}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Meals_shopping_and_cleaning_flow()
    {
        var admin = await RegisteredAsync("house-admin@example.com");
        await admin.PostAsync("/api/families", new CreateFamilyRequest("Familien", "Mor"));
        var inv = await (await admin.PostAsync("/api/invitations", new CreateInvitationRequest(Role.Barn, null))).ReadAsync<CreatedInvitationDto>();
        var child = await RegisteredAsync("house-emma@example.com");
        await child.PostAsync($"/api/invitations/{inv!.Token}/accept", new AcceptInvitationRequest("Emma"));
        var emma = (await (await admin.GetAsync("/api/members")).ReadAsync<List<MemberDto>>())!.Single(m => m.DisplayName == "Emma");

        // Meal plan: admin plans, child sees but cannot change.
        var monday = Today;
        var meal = new SaveMealRequest("Lasagne", "Husk ost", null,
            [new IngredientDto("Hakket oksekød", "500 g"), new IngredientDto("Tomater", "4"), new IngredientDto("Ost", null)]);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"/api/meals/{D(monday)}", meal)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PutAsync($"/api/meals/{D(monday)}", meal)).StatusCode);
        var week = await (await child.GetAsync($"/api/meals?from={D(monday)}&to={D(monday.AddDays(6))}")).ReadAsync<List<MealDto>>();
        Assert.Equal("Lasagne", week!.Single().Title);
        Assert.Equal(3, week!.Single().Ingredients.Count);

        // Save as a dish and use it again.
        var dish = await (await admin.PostAsync($"/api/meals/{D(monday)}/save-as-dish", new { })).ReadAsync<DishDto>();
        Assert.Equal("Lasagne", dish!.Name);
        Assert.Single((await (await child.GetAsync("/api/dishes")).ReadAsync<List<DishDto>>())!);

        // To the shopping list, no duplicates the second time.
        var added = await (await admin.PostAsync("/api/meals/to-shopping-list", new AddMealsToShoppingListRequest(monday, monday.AddDays(6)))).ReadAsync<AddToListResultDto>();
        Assert.Equal(new AddToListResultDto(3, 0), added);
        var again = await (await admin.PostAsync("/api/meals/to-shopping-list", new AddMealsToShoppingListRequest(monday, monday.AddDays(6)))).ReadAsync<AddToListResultDto>();
        Assert.Equal(new AddToListResultDto(0, 3), again);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PostAsync("/api/meals/to-shopping-list", new AddMealsToShoppingListRequest(monday, monday))).StatusCode);

        // Shopping list: grouped data, child can see but not buy.
        var list = (await (await child.GetAsync("/api/shopping")).ReadAsync<List<ShoppingItemDto>>())!;
        Assert.Equal(["Tomater", "Ost", "Hakket oksekød"], list.Select(i => i.Name).ToList()); // fruit/veg, dairy, meat
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PutAsync($"/api/shopping/{list[0].Id}/bought", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/shopping/{list[0].Id}/bought", new { })).StatusCode);

        var milk = await (await admin.PostAsync("/api/shopping", new SaveShoppingItemRequest("Mælk", "2 l", ShoppingCategory.Other, true))).ReadAsync<ShoppingItemDto>();
        Assert.Equal(ShoppingCategory.Dairy, milk!.Category);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/shopping", new SaveShoppingItemRequest("mælk", null, ShoppingCategory.Other, true))).StatusCode);
        var suggestion = await (await child.GetAsync("/api/shopping/category-suggestion?name=toiletpapir")).ReadAsync<CategorySuggestionDto>();
        Assert.Equal(ShoppingCategory.Household, suggestion!.Category);

        // Buying is logged, but one purchase is not a habit yet.
        Assert.Empty((await (await admin.GetAsync("/api/shopping/habits")).ReadAsync<List<HabitDto>>())!);

        // Favorites.
        var fav = await (await admin.PostAsync("/api/shopping/favorites", new SaveShoppingItemRequest("Rugbrød", null, ShoppingCategory.Other, true))).ReadAsync<FavoriteDto>();
        Assert.True(await (await admin.PostAsync($"/api/shopping/favorites/{fav!.Id}/add", new { })).ReadAsync<bool>());
        Assert.False(await (await admin.PostAsync($"/api/shopping/favorites/{fav.Id}/add", new { })).ReadAsync<bool>());
        Assert.Equal(1, await (await admin.DeleteAsync("/api/shopping/bought")).ReadAsync<int>()); // the bought tomatoes

        // Cleaning.
        var created = await (await admin.PostAsync("/api/cleaning", new SaveCleaningRequest("Støvsug stuen", "Stue", 7, emma.Id))).ReadAsync<CleaningDto>();
        Assert.Equal(CleaningStatus.NeverDone, created!.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.PostAsync("/api/cleaning", new SaveCleaningRequest("x", null, 7, null))).StatusCode);
        var done = await (await child.PutAsync($"/api/cleaning/{created.Id}/done", new MarkCleaningDoneRequest(null))).ReadAsync<CleaningDto>();
        Assert.Equal((CleaningStatus.Upcoming, Today), (done!.Status, done.LastDoneOn));
        Assert.Equal("Emma", done.LastDoneByName);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/cleaning/{created.Id}/done", new MarkCleaningDoneRequest(Today.AddDays(1)))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/cleaning/{created.Id}")).StatusCode);
        Assert.Empty((await (await admin.GetAsync("/api/cleaning")).ReadAsync<List<CleaningDto>>())!);
    }
}

