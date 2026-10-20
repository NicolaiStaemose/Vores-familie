using VoresFamilie.Web.Api;

namespace VoresFamilie.Web;

/// <summary>Holds the signed-in user and family for the current page session.</summary>
public sealed class Session(IApiClient api)
{
    public MeDto? Me { get; private set; }
    public bool Loaded { get; private set; }
    public bool IsSignedIn => Me is not null;
    public FamilyDto? Family => Me?.Family;
    public bool IsAdmin => Family?.Me.Role == Role.Admin;

    public event Action? Changed;

    public async Task RefreshAsync()
    {
        try { Me = await api.GetMeAsync(); }
        catch (ApiException ex) when (ex.StatusCode == 401) { Me = null; }
        Loaded = true;
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        await api.LogoutAsync();
        Me = null;
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

