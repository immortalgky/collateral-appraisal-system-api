using System.Text.Json;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>
/// Serializes and deserializes the 14 polymorphic method-detail shapes to/from JSON.
/// Kept at the domain layer so it is available to both the application and infrastructure layers.
/// </summary>
public static class MethodDetailSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(object detail)
    {
        return JsonSerializer.Serialize(detail, detail.GetType(), Options);
    }

    /// <summary>
    /// Deserializes <paramref name="json"/> into the strongly-typed record for the given
    /// <paramref name="methodTypeCode"/> ("01"–"14"). Returns <c>null</c> if the code is unknown.
    /// </summary>
    public static object? Deserialize(string methodTypeCode, string json)
    {
        return methodTypeCode switch
        {
            "01" => JsonSerializer.Deserialize<Method01Detail>(json, Options),
            "02" => JsonSerializer.Deserialize<Method02Detail>(json, Options),
            "03" => JsonSerializer.Deserialize<Method03Detail>(json, Options),
            "04" => JsonSerializer.Deserialize<Method04Detail>(json, Options),
            "05" => JsonSerializer.Deserialize<Method05Detail>(json, Options),
            "06" => JsonSerializer.Deserialize<Method06Detail>(json, Options),
            "07" => JsonSerializer.Deserialize<Method07Detail>(json, Options),
            "08" => JsonSerializer.Deserialize<Method08Detail>(json, Options),
            "09" => JsonSerializer.Deserialize<Method09Detail>(json, Options),
            "10" => JsonSerializer.Deserialize<Method10Detail>(json, Options),
            "11" => JsonSerializer.Deserialize<Method11Detail>(json, Options),
            "12" => JsonSerializer.Deserialize<Method12Detail>(json, Options),
            "13" => JsonSerializer.Deserialize<Method13Detail>(json, Options),
            "14" => JsonSerializer.Deserialize<Method14Detail>(json, Options),
            _ => null
        };
    }

    /// <summary>
    /// Deserializes to the strongly-typed record and casts to <typeparamref name="T"/>.
    /// Throws <see cref="InvalidOperationException"/> if the code does not match T.
    /// </summary>
    public static T Deserialize<T>(string methodTypeCode, string json) where T : class
    {
        var result = Deserialize(methodTypeCode, json)
            ?? throw new InvalidOperationException($"Unknown method type code: {methodTypeCode}");

        return result as T
            ?? throw new InvalidOperationException(
                $"Method type code '{methodTypeCode}' does not deserialize to {typeof(T).Name}");
    }
}
