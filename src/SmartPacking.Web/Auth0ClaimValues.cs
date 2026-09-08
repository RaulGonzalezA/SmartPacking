using System.Text.Json;

namespace SmartPacking.Web;

public static class Auth0ClaimValues
{
    public static IReadOnlyList<string> Deserialize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray(),
                JsonValueKind.String => [document.RootElement.GetString()!],
                _ => [value],
            };
        }
        catch (JsonException)
        {
            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
