namespace Request.Contracts.Requests.Dtos;

public record PropertyDto
(
    string PropertyType,
    string BuildingType,
    decimal? SellingPrice
);