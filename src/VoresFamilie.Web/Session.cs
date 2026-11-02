using VoresFamilie.Web.Api;

namespace VoresFamilie.Web;

/// <summary>Holds the signed-in user and family for the current page session.</summary>
public sealed class Session(IApiClient api, OfflineCache cache)
{
    public MeDto? Me { get; private set; }
    public bool Loaded { get; private set; }
    /// <summary>True when there is no connection and who is signed in comes from the saved copy.</summary>
    public bool Offline { get; private set; }
    public bool IsSignedIn => Me is not null;
    public FamilyDto? Family => Me?.Family;
    public bool IsAdmin => Family?.Me.Role == Role.Admin;

    /// <summary>Whether the signed-in member may change things in an area (admins always can).</summary>
    public bool CanEdit(Area area) =>
        Family?.Me.Permissions.TryGetValue(area.ToString(), out var level) == true && level == AccessLevel.Edit;

    public event Action? Changed;
    /// <summary>Raised when the saved offline copy has been removed (log out, or the sign-in ended).</summary>
    public event Action? LoggedOut;

    public async Task RefreshAsync()
    {
        try
        {
            Me = await api.GetMeAsync();
            Offline = false;
            await SaveMeAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            // Signed out for real (not just offline): the saved copy must not survive on a shared phone.
            Me = null;
            Offline = false;
            await cache.ClearAsync();
            LoggedOut?.Invoke();
        }
        catch (HttpRequestException)
        {
            // No connection: stay signed in with the last copy, so the app opens in the shop or on the train.
            Me = await cache.LoadAsync<MeDto>(OfflineCache.Keys.Me);
            Offline = Me is not null;
        }
        Loaded = true;
        Changed?.Invoke();
    }

    private async Task SaveMeAsync()
    {
        // A different person on this phone must not see the previous person's saved data.
        var saved = await cache.LoadAsync<MeDto>(OfflineCache.Keys.Me);
        if (saved is not null && saved.UserId != Me?.UserId)
        {
            await cache.ClearAsync();
            LoggedOut?.Invoke();
        }
        if (Me is not null) await cache.SaveAsync(OfflineCache.Keys.Me, Me);
    }

    public async Task LogoutAsync()
    {
        await api.LogoutAsync();
        Me = null;
        Offline = false;
        await cache.ClearAsync();
        LoggedOut?.Invoke();
        Changed?.Invoke();
    }

    public static string ErrorMessage(Exception ex) => ex switch
    {
        ApiException<ErrorDto> e => e.Result.Message,
        ApiException { Response: { Length: > 0 } body } when TryReadMessage(body, out var message) => message,
        ApiException { StatusCode: 403 } => "Du har ikke adgang til at gøre dette.",
        ApiException { StatusCode: 401 } => "Du er ikke logget ind.",
        ApiException or HttpRequestException => "Der kunne ikke oprettes forbindelse til serveren.",
        _ => "Noget gik galt.",
    };

    private static bool TryReadMessage(string body, out string message)
    {
        message = "";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } text)
            {
                message = text;
                return true;
            }
        }
        catch (System.Text.Json.JsonException) { }
        return false;
    }

    public static string RoleName(Role r) => r switch { Role.Admin => "Admin", Role.Voksen => "Voksen", _ => "Barn" };

    public static string AreaName(Area a) => a switch
    {
        Area.Calendars => "Kalendere",
        Area.Homework => "Lektier",
        Area.MealPlan => "Madplan",
        Area.Shopping => "Indkøb",
        Area.Routines => "Rutiner",
        Area.Cleaning => "Rengøring",
        _ => "ToDo",
    };

    public static string LevelName(AccessLevel l) => l switch
    {
        AccessLevel.None => "Ingen adgang",
        AccessLevel.View => "Kan se",
        _ => "Kan se og redigere",
    };
}


