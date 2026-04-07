namespace Appraisal.Application.Features.PricingAnalysis.SaveProfitRentAnalysis;

public record SaveProfitRentAnalysisRequest(
    decimal MarketRentalFeePerSqWa,
    string GrowthRateType,
    decimal GrowthRatePercent,
    int GrowthIntervalYears,
    decimal DiscountRate,
    bool IncludeBuildingCost,
    IReadOnlyList<ProfitRentGrowthPeriodInput>? GrowthPeriods = null,
    string? Remark = null,
    decimal? EstimatePriceRounded = null,
    decimal? AppraisalPriceWithBuildingRounded = null
);

public record ProfitRentGrowthPeriodInput(
    int FromYear,
    int ToYear,
    decimal GrowthRatePercent
);
