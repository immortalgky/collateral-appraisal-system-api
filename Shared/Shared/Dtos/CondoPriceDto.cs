namespace Shared.Dtos;

public record CondoPriceDto(
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice
);
