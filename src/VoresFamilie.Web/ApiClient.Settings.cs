using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoresFamilie.Web.Api;

public partial class ApiClient
{
    // The OpenAPI contract has string enums; enum values inside dictionaries get no per-property converter, so register one globally.
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
        settings.Converters.Add(new JsonStringEnumConverter());
}
