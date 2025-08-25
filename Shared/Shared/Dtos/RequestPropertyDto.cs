namespace Shared.Dtos;

public record RequestPropertyDto(
    string PropertyType,
    string BuildingType,
    decimal? SellingPrice
);