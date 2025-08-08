namespace Request.Contracts.Requests.Dtos;

public record RequestPropertyDto(
    string PropertyType,
    string BuildingType,
    decimal? SellingPrice
);