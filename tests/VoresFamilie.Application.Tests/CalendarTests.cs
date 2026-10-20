using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Application.Tests;

public class CalendarSyncTests
{
    [Fact]
    public async Task First_sync_pulls_events_and_later_syncs_only_changes()
    {
        var env = new Env();
        env.Google.AddCalendar("g1", "Fælles");
        env.Google.Put("g1", "Svømning", env.Day(1, 16), env.Day(1, 17), id: "e1");
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);

        Assert.Single(await env.EventsAsync(Env.AdminUser, cal.Id));

        env.Google.Put("g1", "Svømning (flyttet)", env.Day(2, 16), env.Day(2, 17), id: "e1"); // changed elsewhere
        env.Google.Put("g1", "Ny", env.Day(3, 9), env.Day(3, 10), id: "e2");
        env.Time.UtcNow += TimeSpan.FromMinutes(5);
        var events = await env.EventsAsync(Env.AdminUser, cal.Id);
        Assert.Equal(["Svømning (flyttet)", "Ny"], events.Select(e => e.Title));

        env.Google.Remove("g1", "e1");
        env.Time.UtcNow += TimeSpan.FromMinutes(5);
        Assert.Equal(["Ny"], (await env.EventsAsync(Env.AdminUser, cal.Id)).Select(e => e.Title));
    }

    [Fact]
    public async Task Expired_sync_token_triggers_full_resync()
    {
        var env = new Env();
        env.Google.AddCalendar("g1", "Fælles");
        env.Google.Put("g1", "A", env.Day(1), env.Day(1, 1), id: "a");
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        await env.EventsAsync(Env.AdminUser);

        env.Google.Put("g1", "B", env.Day(2), env.Day(2, 1), id: "b");
        env.Google.ExpireSyncTokens();
        env.Time.UtcNow += TimeSpan.FromMinutes(5);

        Assert.Equal(2, (await env.EventsAsync(Env.AdminUser, cal.Id)).Count);
        Assert.Equal(2, env.Calendars.Events.Count); // no duplicates after rebuilding the cache
    }

    [Fact]
    public async Task Deleted_event_also_removes_its_homework_completion()
    {
        var env = new Env();
        env.Google.AddCalendar("hw", "Emma lektier");
        env.Google.Put("hw", "Lektie: Dansk", env.Day(1, 15), env.Day(1, 16), id: "h1");
        var cal = await env.AddAsync("hw", "Emma lektier", CalendarKind.Homework, env.Child.Id);
        await env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "h1", true);
        Assert.Single(env.Calendars.Completions);

        env.Google.Remove("hw", "h1");
        env.Time.UtcNow += TimeSpan.FromMinutes(5);
        await env.EventsAsync(Env.AdminUser);
        Assert.Empty(env.Calendars.Completions);
    }

    [Fact]
    public async Task Recent_cache_is_not_synced_again()
    {
        var env = new Env();
        env.Google.AddCalendar("g1", "Fælles");
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        await env.EventsAsync(Env.AdminUser);
        env.Google.Put("g1", "Sent tilføjet", env.Day(1), env.Day(1, 1));
        env.Time.UtcNow += TimeSpan.FromSeconds(30);
        Assert.Empty(await env.EventsAsync(Env.AdminUser, cal.Id)); // still within the 2 minute cache window
        var result = await env.CalendarService.SyncNowAsync(Env.AdminUser);
        Assert.Equal(0, result.Failed);
        Assert.Single(await env.EventsAsync(Env.AdminUser, cal.Id));
    }
}

public class CalendarServiceTests
{
    [Fact]
    public async Task Members_can_only_add_their_own_personal_calendar()
    {
        var env = new Env();
        env.Google.AddCalendar("g1", "Fælles");
        env.Google.AddCalendar("g2", "Emma privat");
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            env.CalendarService.AddCalendarAsync(Env.ChildUser, new AddCalendarRequest("g1", "Fælles", CalendarKind.Family, null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            env.CalendarService.AddCalendarAsync(Env.ChildUser, new AddCalendarRequest("g1", "Noahs", CalendarKind.Personal, env.Sibling.Id, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            env.CalendarService.AddCalendarAsync(Env.ChildUser, new AddCalendarRequest("g1", "Lektier", CalendarKind.Homework, env.Child.Id, null)));

        var own = await env.CalendarService.AddCalendarAsync(Env.ChildUser, new AddCalendarRequest("g2", "Emma privat", CalendarKind.Personal, null, null));
        Assert.Equal(env.Child.Id, own.OwnerMemberId);
        Assert.Equal("Emma", own.OwnerName);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.CalendarService.RemoveCalendarAsync(Env.SiblingUser, own.Id));
        await env.CalendarService.RemoveCalendarAsync(Env.ChildUser, own.Id);
    }

    [Fact]
    public async Task Same_google_calendar_cannot_be_added_twice_and_must_exist()
    {
        var env = new Env();
        await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        await Assert.ThrowsAsync<DomainException>(() => env.AddAsync("g1", "Igen", CalendarKind.Family));
        await Assert.ThrowsAsync<DomainException>(() =>
            env.CalendarService.AddCalendarAsync(Env.AdminUser, new AddCalendarRequest("findes-ikke", "X", CalendarKind.Family, null, null)));
    }

    [Fact]
    public async Task Child_can_view_but_not_create_events_in_family_calendar()
    {
        var env = new Env();
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        var req = new SaveEventRequest("Fest", null, null, env.Day(1, 18), env.Day(1, 22), false);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.CalendarService.CreateEventAsync(Env.ChildUser, cal.Id, req));
        Assert.Single(await env.CalendarService.ListCalendarsAsync(Env.ChildUser));
    }

    [Fact]
    public async Task Created_event_is_written_to_google_and_cached()
    {
        var env = new Env();
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        var created = await env.CalendarService.CreateEventAsync(Env.AdminUser, cal.Id,
            new SaveEventRequest("Fødselsdag", "Kage", "Hjemme", env.Day(2, 14), env.Day(2, 16), false));

        Assert.Equal(1, env.Google.EventCount("g1"));
        Assert.Equal("Fødselsdag", (await env.EventsAsync(Env.AdminUser, cal.Id)).Single().Title);
        Assert.Equal(created.Id, env.Calendars.Events.Single().GoogleEventId);
    }

    [Fact]
    public async Task Update_and_delete_go_through_to_google()
    {
        var env = new Env();
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        var e = await env.CalendarService.CreateEventAsync(Env.AdminUser, cal.Id,
            new SaveEventRequest("A", null, null, env.Day(1), env.Day(1, 1), false));

        var updated = await env.CalendarService.UpdateEventAsync(Env.AdminUser, cal.Id, e.Id,
            new SaveEventRequest("B", null, null, env.Day(1), env.Day(1, 2), false));
        Assert.Equal("B", updated.Title);

        await env.CalendarService.DeleteEventAsync(Env.AdminUser, cal.Id, e.Id);
        Assert.Equal(0, env.Google.EventCount("g1"));
        Assert.Empty(env.Calendars.Events);
    }

    [Fact]
    public async Task Update_conflict_is_reported_and_cache_refreshed()
    {
        var env = new Env();
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        var e = await env.CalendarService.CreateEventAsync(Env.AdminUser, cal.Id,
            new SaveEventRequest("Original", null, null, env.Day(1), env.Day(1, 1), false));

        env.Google.Put("g1", "Ændret et andet sted", env.Day(1), env.Day(1, 1), id: e.Id); // new ETag in Google
        env.Time.UtcNow += TimeSpan.FromMinutes(5);

        await Assert.ThrowsAsync<ConflictException>(() => env.CalendarService.UpdateEventAsync(Env.AdminUser, cal.Id, e.Id,
            new SaveEventRequest("Mit forsøg", null, null, env.Day(1), env.Day(1, 1), false)));
        Assert.Equal("Ændret et andet sted", (await env.EventsAsync(Env.AdminUser, cal.Id)).Single().Title);
    }

    [Fact]
    public async Task Personal_calendar_owner_can_edit_it_even_without_edit_rights()
    {
        var env = new Env();
        env.Family.SetPermission(env.Child.Id, Area.Calendars, AccessLevel.View);
        var cal = await env.AddAsync("p1", "Emma privat", CalendarKind.Personal, env.Child.Id);
        var created = await env.CalendarService.CreateEventAsync(Env.ChildUser, cal.Id,
            new SaveEventRequest("Veninde", null, null, env.Day(1), env.Day(1, 1), false));
        Assert.Equal("Veninde", created.Title);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.CalendarService.CreateEventAsync(Env.SiblingUser, cal.Id,
            new SaveEventRequest("Spøg", null, null, env.Day(1), env.Day(1, 1), false)));
    }

    [Fact]
    public async Task Calendar_without_view_access_is_hidden()
    {
        var env = new Env();
        var cal = await env.AddAsync("g1", "Fælles", CalendarKind.Family);
        env.Family.SetPermission(env.Child.Id, Area.Calendars, AccessLevel.None);
        Assert.Empty(await env.CalendarService.ListCalendarsAsync(Env.ChildUser));
        Assert.Empty(await env.EventsAsync(Env.ChildUser));
        await Assert.ThrowsAsync<NotFoundException>(() => env.CalendarService.CreateEventAsync(Env.ChildUser, cal.Id,
            new SaveEventRequest("X", null, null, env.Day(1), env.Day(1, 1), false)));
    }

    [Fact]
    public async Task Schedule_calendar_is_shown_to_the_child_but_is_read_only()
    {
        var env = new Env();
        env.Google.AddCalendar("sk", "Emma skema");
        env.Google.Put("sk", "Dansk", env.Day(1, 8), env.Day(1, 9), id: "s1");
        var cal = await env.AddAsync("sk", "Emma skema", CalendarKind.Schedule, env.Child.Id);

        Assert.Equal(CalendarKind.Schedule, (await env.CalendarService.ListCalendarsAsync(Env.ChildUser)).Single().Kind);
        Assert.Equal("Dansk", (await env.EventsAsync(Env.ChildUser, cal.Id)).Single().Title);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.CalendarService.DeleteEventAsync(Env.ChildUser, cal.Id, "s1"));
        Assert.Empty(await env.Homework.ListAsync(Env.ChildUser, env.Day(0), env.Day(7), null)); // not homework
    }

    [Fact]
    public async Task Removing_a_calendar_removes_cache_and_completions()
    {
        var env = new Env();
        env.Google.AddCalendar("hw", "Emma lektier");
        env.Google.Put("hw", "Lektie", env.Day(1, 15), env.Day(1, 16), id: "h1");
        var cal = await env.AddAsync("hw", "Emma lektier", CalendarKind.Homework, env.Child.Id);
        await env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "h1", true);

        await env.CalendarService.RemoveCalendarAsync(Env.AdminUser, cal.Id);
        Assert.Empty(env.Calendars.Calendars);
        Assert.Empty(env.Calendars.Events);
        Assert.Empty(env.Calendars.Completions);
    }
}

public class HomeworkServiceTests
{
    private static async Task<(Env, CalendarDto)> SetupAsync()
    {
        var env = new Env();
        env.Google.AddCalendar("hw", "Emma lektier");
        env.Google.Put("hw", "Lektie: Dansk", env.Day(1, 15), env.Day(1, 16), "Læs side 4\n\nBarn: Emma\nFag: Dansk", id: "h1");
        env.Google.Put("hw", "99 arter", env.Day(1, 8), env.Day(1, 9), "Aktivitet", id: "x1");
        env.Google.Put("hw", "LEKTIE: Matematik", env.Day(2, 15), env.Day(2, 16), "Opg. 3\nFag: Matematik", id: "h2");
        var cal = await env.AddAsync("hw", "Emma lektier", CalendarKind.Homework, env.Child.Id);
        return (env, cal);
    }

    [Fact]
    public async Task Lists_only_homework_events_with_subject()
    {
        var (env, _) = await SetupAsync();
        var items = await env.Homework.ListAsync(Env.ChildUser, env.Day(0), env.Day(7), null);
        Assert.Equal(["h1", "h2"], items.Select(i => i.EventId));
        Assert.Equal(["Dansk", "Matematik"], items.Select(i => i.Subject));
        Assert.All(items, i => { Assert.Equal("Emma", i.ChildName); Assert.False(i.Done); Assert.True(i.CanCheck); });
    }

    [Fact]
    public async Task Other_events_in_the_homework_calendar_show_as_normal_events()
    {
        var (env, cal) = await SetupAsync();
        var all = await env.EventsAsync(Env.ChildUser, cal.Id);
        Assert.Equal(3, all.Count);
        Assert.Equal(2, all.Count(e => e.IsHomework));
    }

    [Fact]
    public async Task Child_can_tick_and_untick_own_homework()
    {
        var (env, cal) = await SetupAsync();
        await env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "h1", true);
        await env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "h1", true); // idempotent
        var item = (await env.Homework.ListAsync(Env.ChildUser, env.Day(0), env.Day(7), null)).First(i => i.EventId == "h1");
        Assert.True(item.Done);
        Assert.Single(env.Calendars.Completions);

        await env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "h1", false);
        Assert.Empty(env.Calendars.Completions);
    }

    [Fact]
    public async Task Sibling_cannot_tick_others_homework_but_admin_can()
    {
        var (env, cal) = await SetupAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Homework.SetDoneAsync(Env.SiblingUser, cal.Id, "h1", true));
        await env.Homework.SetDoneAsync(Env.AdminUser, cal.Id, "h1", true);
        Assert.Single(env.Calendars.Completions);
    }

    [Fact]
    public async Task Child_blocked_from_checking_own_items_is_refused()
    {
        var (env, cal) = await SetupAsync();
        env.Family.SetCanCheckOwnItems(env.Child.Id, false);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "h1", true));
        var item = (await env.Homework.ListAsync(Env.ChildUser, env.Day(0), env.Day(7), null)).First();
        Assert.False(item.CanCheck);
    }

    [Fact]
    public async Task Non_homework_event_cannot_be_ticked()
    {
        var (env, cal) = await SetupAsync();
        await env.EventsAsync(Env.AdminUser);
        await Assert.ThrowsAsync<NotFoundException>(() => env.Homework.SetDoneAsync(Env.ChildUser, cal.Id, "x1", true));
    }

    [Fact]
    public async Task Filter_by_child()
    {
        var (env, _) = await SetupAsync();
        Assert.Empty(await env.Homework.ListAsync(Env.AdminUser, env.Day(0), env.Day(7), env.Sibling.Id));
        Assert.Equal(2, (await env.Homework.ListAsync(Env.AdminUser, env.Day(0), env.Day(7), env.Child.Id)).Count);
    }
}


