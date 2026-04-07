namespace Appraisal.Application.Features.PricingAnalysis.GetLeaseholdAnalysis;

public record GetLeaseholdAnalysisResult(LeaseholdAnalysisDto? Analysis, string? Remark);

public record LeaseholdAnalysisDto(
    Guid Id,
    Guid PricingMethodId,
    decimal LandValuePerSqWa,
    string LandGrowthRateType,
    decimal LandGrowthRatePercent,
    int LandGrowthIntervalYears,
    decimal ConstructionCostIndex,
    decimal InitialBuildingValue,
    decimal DepreciationRate,
    int DepreciationIntervalYears,
    int BuildingCalcStartYear,
    decimal DiscountRate,
    decimal TotalIncomeOverLeaseTerm,
    decimal ValueAtLeaseExpiry,
    decimal FinalValue,
    decimal FinalValueRounded,
    bool IsPartialUsage,
    decimal? PartialRai,
    decimal? PartialNgan,
    decimal? PartialWa,
    decimal? PartialLandArea,
    decimal? PricePerSqWa,
    decimal? PartialLandPrice,
    decimal? EstimateNetPrice,
    decimal? EstimatePriceRounded,
    List<LandGrowthPeriodDto> LandGrowthPeriods,
    List<LeaseholdCalculationDetailDto> CalculationDetails
);

public record LandGrowthPeriodDto(
    Guid Id,
    int FromYear,
    int ToYear,
    decimal GrowthRatePercent
);

public record LeaseholdCalculationDetailDto(
    decimal Year,
    decimal LandValue,
    decimal LandGrowthPercent,
    decimal BuildingValue,
    decimal DepreciationAmount,
    decimal DepreciationPercent,
    decimal BuildingAfterDepreciation,
    decimal TotalLandAndBuilding,
    decimal RentalIncome,
    decimal PvFactor,
    decimal NetCurrentRentalIncome
);
