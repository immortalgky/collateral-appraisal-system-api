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

    /// <summary>Property / <c>AdditionalConfiguration</c> key naming the activity whose completer a strategy reuses.</summary>
    public const string SameAssigneeAsActivityKey = "sameAssigneeAsActivity";

    /// <summary>
    /// Overlays <paramref name="overrides"/> on <paramref name="baseline"/> per key (override wins), skipping
    /// values that are not set (see <see cref="IsSet"/>) so they never erase a baseline value. Returns the
    /// baseline itself when there is nothing to overlay; otherwise a copy, never mutating the baseline.
    /// </summary>
    public static Dictionary<string, object> Overlay(
        Dictionary<string, object> baseline, Dictionary<string, object>? overrides)
    {
        if (overrides is not { Count: > 0 }) return baseline;

        var merged = new Dictionary<string, object>(baseline);
        foreach (var (key, value) in overrides)
            if (IsSet(value))
                merged[key] = value;
        return merged;
    }

    /// <summary>
    /// False for null, a JSON null, or an empty/whitespace string: the "this override value is not set" rule
    /// shared by the property overlay, admin validation and the followup-selection activity.
    /// </summary>
    public static bool IsSet(object? value) => value switch
    {
        null => false,
        string s => !string.IsNullOrWhiteSpace(s),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => false,
        JsonElement { ValueKind: JsonValueKind.String } je => !string.IsNullOrWhiteSpace(je.GetString()),
        _ => true
    };

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
