using System.Text.Json.Serialization;

namespace Request.Contracts.Requests.Dtos;

public record RequestPropertyDto(
    string? PropertyType,
    string? BuildingType,
    [property: JsonConverter(typeof(NullableDecimalEmptyStringConverter))]
    decimal? SellingPrice
);