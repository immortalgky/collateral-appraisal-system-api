using System.Text.Json;

namespace Shared.Extensions;

public static class JsonStringArrayExtensions
{
    /// <summary>
    /// Parses a column stored as a JSON array of strings (e.g. Company.LoanTypes): null/blank elements are
    /// dropped and the rest trimmed. Null, blank or malformed input gives an empty list.
    /// </summary>
    public static List<string> ParseJsonStringArray(this string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return (JsonSerializer.Deserialize<List<string?>>(json) ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
