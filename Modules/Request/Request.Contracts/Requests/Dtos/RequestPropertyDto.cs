using System.Text.Json.Serialization;

namespace Request.Contracts.Requests.Dtos;

public record RequestPropertyDto(
    string? PropertyType,
    string? BuildingType,
    string? BuildingTypeOther,
    [property: JsonConverter(typeof(NullableDecimalEmptyStringConverter))]
    decimal? SellingPrice
);