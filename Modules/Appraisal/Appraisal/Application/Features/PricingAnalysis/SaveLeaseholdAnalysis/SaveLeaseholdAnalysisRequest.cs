namespace Appraisal.Application.Features.PricingAnalysis.SaveLeaseholdAnalysis;

public record SaveLeaseholdAnalysisRequest(
    // Input fields
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
    // Growth periods (for Period mode)
    IReadOnlyList<LandGrowthPeriodInput>? LandGrowthPeriods,
    // Partial usage
    bool IsPartialUsage = false,
    decimal? PartialRai = null,
    decimal? PartialNgan = null,
    decimal? PartialWa = null,
    decimal? PricePerSqWa = null,
    // Remark
    string? Remark = null,
    // Override for estimated price
    decimal? EstimatePriceRounded = null
);

public record LandGrowthPeriodInput(
    int FromYear,
    int ToYear,
    decimal GrowthRatePercent
);
