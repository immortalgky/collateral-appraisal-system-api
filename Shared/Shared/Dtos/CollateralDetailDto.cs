namespace Shared.Dtos;

public record CollateralDetailDto(
    string? EngineNo,
    string? RegistrationNo,
    int? YearOfManufacture,
    string? CountryOfManufacture,
    DateTime? PurchaseDate,
    decimal? PurchasePrice
);