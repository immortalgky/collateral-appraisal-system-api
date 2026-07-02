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
    decimal? AppraisalPrice = null,
    // User-overridden adjusted final value (stored as-is; never recomputed)
    decimal? FinalValueAdjusted = null
);

public record ProfitRentGrowthPeriodInput(
    int FromYear,
    int ToYear,
    decimal GrowthRatePercent
);
