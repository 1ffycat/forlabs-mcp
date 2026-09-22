using System.Text.Json;
using System.Text.Json.Nodes;

namespace ForlabsMcp;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Pretty(object? value) => JsonSerializer.Serialize(value, Indented);

    public static string Pretty(JsonNode? node) => node?.ToJsonString(Indented) ?? "null";

    public static IEnumerable<JsonObject> ArrayOf(JsonNode? node, string property) =>
        node?[property]?.AsArray()
            .Select(n => n as JsonObject)
            .Where(n => n is not null)
            .Select(n => n!)
        ?? [];

    public static string? Str(this JsonObject? o, string key) => (string?)o?[key];
    public static int? Int(this JsonObject? o, string key) => (int?)o?[key];
}
