using System.Text.Json;

namespace Shared.Extensions;

public static class JsonElementExtensions
{
    /// <summary>
    /// Converts a JsonElement to a Dictionary&lt;string, object&gt; with proper type conversion
    /// </summary>
    public static Dictionary<string, object> ToDictionary(this JsonElement jsonElement)
    {
        var result = new Dictionary<string, object>();

        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in jsonElement.EnumerateObject())
        {
            result[property.Name] = ConvertJsonElement(property.Value);
        }

        return result;
    }

    /// <summary>
    /// Converts JsonElement values in a dictionary to proper .NET types
    /// </summary>
    public static Dictionary<string, object> ConvertJsonElements(this Dictionary<string, object> dictionary)
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in dictionary)
        {
            if (kvp.Value is JsonElement jsonElement)
            {
                result[kvp.Key] = ConvertJsonElement(jsonElement);
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Object => element.ToDictionary(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            _ => element.ToString()
        };
    }
}