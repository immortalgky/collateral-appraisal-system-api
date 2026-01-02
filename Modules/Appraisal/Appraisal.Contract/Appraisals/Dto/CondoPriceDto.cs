namespace Appraisal.Contracts.Appraisals.Dto;

public record CondoPriceDto(
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice
);
