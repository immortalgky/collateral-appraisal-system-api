namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingAppraisalDepreciationDetailDto(
    Guid Id,
    string? AreaDescription,
    decimal Area,
    decimal PricePerSqMBeforeDepreciation,
    decimal PriceBeforeDepreciation,
    short Year,
    bool IsBuilding,
    string DepreciationMethod,
    decimal DepreciationYearPct,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation,
    decimal PricePerSqMAfterDepreciation,
    decimal PriceAfterDepreciation,
    IReadOnlyList<BuildingAppraisalDepreciationPeriodDto> DepreciationPeriods
);
