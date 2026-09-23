using System.Text.Json;

namespace PascalRuntime;

// Flat JSON helpers exposed to Pascal as scalar-only functions (no JSON object type
// exists in the language) — enough to read a simple {"key": value, ...} request body
// and to safely quote/escape a string being written into a JSON response.
public static class Json
{
    public static string GetString(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(key, out var val))
                return val.GetString() ?? "";
        }
        catch (JsonException) { }
        return "";
    }

    public static int GetInt(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(key, out var val))
                return val.GetInt32();
        }
        catch (JsonException) { }
        return 0;
    }

    // Returns the value as a valid, quoted JSON string literal (e.g. Santiago -> "Santiago"),
    // so callers write HttpWrite('{"name": ' + JsonEscape(name) + '}') safely.
    public static string Escape(string s) => JsonSerializer.Serialize(s);
}
