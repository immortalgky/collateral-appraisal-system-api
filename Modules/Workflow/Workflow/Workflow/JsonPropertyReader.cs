using System.Text.Json;

namespace Workflow.Workflow;

/// <summary>
/// Reads values out of a <see cref="Dictionary{TKey,TValue}"/> whose values may be raw CLR types or
/// <see cref="JsonElement"/> (the shape produced when activity properties are deserialized from the
/// workflow-definition JSON). Shared by the assignment pipeline and the admin activity-picker endpoint
/// so the JsonElement-handling logic lives in one place.
/// </summary>
public static class JsonPropertyReader
{
    /// <summary>Returns null for null/empty strings, otherwise the value unchanged.</summary>
    public static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    public static string? GetString(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var val) || val is null) return null;
        if (val is string s) return s;
        if (val is JsonElement { ValueKind: JsonValueKind.String } je) return je.GetString();
        return val.ToString();
    }

    public static List<string> GetStringList(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var val) || val is null) return [];
        if (val is List<string> list) return list;
        if (val is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
                return je.EnumerateArray().Select(e => e.GetString() ?? "").Where(x => x.Length > 0).ToList();
            if (je.ValueKind == JsonValueKind.String && je.GetString() is { Length: > 0 } single)
                return [single];
        }
        if (val is string str && str.Length > 0) return [str];
        return [];
    }
}
