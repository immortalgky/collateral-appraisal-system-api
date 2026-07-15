using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Request.Contracts.Requests.Dtos;

/// <summary>
/// Defense-in-depth JSON binding leniency: System.Text.Json throws a JsonException when an
/// empty string ("") is submitted for a nullable decimal field (e.g. properties[].sellingPrice
/// on a draft request), surfacing as a raw 400 data-type error instead of a normal validation
/// message. Treat "" / whitespace-only strings as null; numeric and null tokens behave as usual.
/// </summary>
public class NullableDecimalEmptyStringConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return string.IsNullOrWhiteSpace(stringValue)
                ? null
                : decimal.Parse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
