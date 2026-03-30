namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingAppraisalDepreciationPeriodDto(
    Guid Id,
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation
);
