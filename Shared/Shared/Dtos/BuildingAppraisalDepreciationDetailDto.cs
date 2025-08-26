namespace Shared.Dtos;

public record BuildingAppraisalDepreciationDetailDto(
    string AreaDesc,
    decimal Area,
    decimal PricePerSqM,
    decimal PriceBeforeDegradation,
    short Year,
    decimal DegradationYearPct,
    decimal TotalDegradationPct,
    decimal PriceDegradation,
    decimal TotalPrice,
    bool? AppraisalMethod,
    IReadOnlyList<BuildingAppraisalDepreciationPeriodDto> BuildingAppraisalDepreciationPeriods
);
