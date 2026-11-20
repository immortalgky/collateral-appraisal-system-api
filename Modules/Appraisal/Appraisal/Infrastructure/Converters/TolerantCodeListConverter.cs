using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Appraisal.Infrastructure.Converters;

/// <summary>
/// A list of parameter codes stored as a JSON array, that also reads what the column held before it became a list:
/// a bare code ("01") or any other bare text becomes a one-item list, blank becomes null. It always writes an array.
/// </summary>
/// <remarks>
/// ponytail: used for LandCheckMethodType only. A plain JSON converter throws on a bare "01" (not valid JSON), and the
/// column already holds bare codes and a few junk values, so this reads them as they are instead of needing a rewrite
/// of the data. Drop it for the standard converter once every row has been saved as an array.
/// </remarks>
public sealed class TolerantCodeListConverter : ValueConverter<List<string>?, string?>
{
    public TolerantCodeListConverter()
        : base(v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), v => Parse(v))
    {
    }

    internal static List<string>? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(s, (JsonSerializerOptions?)null);
            }
            catch (JsonException)
            {
                // not an array after all: fall through and keep the text as it is
            }
        }

        return [s];
    }
}
