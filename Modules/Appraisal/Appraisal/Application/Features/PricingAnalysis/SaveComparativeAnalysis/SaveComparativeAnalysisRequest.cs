namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

/// <summary>
/// Request to save the entire comparative analysis (Step 1 + Step 2) in a single transaction
/// </summary>
public record SaveComparativeAnalysisRequest(
    IReadOnlyList<ComparativeFactorInput> ComparativeFactors,
    IReadOnlyList<FactorScoreInput> FactorScores,
    IReadOnlyList<CalculationInput> Calculations,
    Guid? ComparativeAnalysisTemplateId = null,
    decimal? AppraisalValue = null
);

/// <summary>
/// Step 1 - Factor selection for comparison
/// Id = null for new items, existing Id for updates
/// </summary>
public record ComparativeFactorInput(
    Guid? Id, // null = create new, existing = update
    Guid FactorId,
    int DisplaySequence,
    bool IsSelectedForScoring,
    string? Remarks = null
);

/// <summary>
/// Step 2 - Factor scoring per comparable
/// MarketComparableId = null means this is for the Collateral property
/// Id = null for new items, existing Id for updates
/// </summary>
public record FactorScoreInput(
    Guid? Id, // null = create new, existing = update
    Guid FactorId,
    Guid? MarketComparableId, // null = Collateral
    decimal FactorWeight,
    int DisplaySequence,
    string? Value = null,
    decimal? Score = null,
    decimal? Intensity = null, // WQS only
    decimal? AdjustmentPct = null,
    decimal? AdjustmentAmt = null, // SaleGrid/DirectComparison
    string? ComparisonResult = null, // SaleGrid/DirectComparison: Equal/Inferior/Superior
    string? Remarks = null
);

/// <summary>
/// Pricing calculation data per comparable
/// </summary>
public record CalculationInput(
    Guid MarketComparableId,
    decimal? OfferingPrice = null,
    string? OfferingPriceUnit = null,
    decimal? AdjustOfferPricePct = null,
    decimal? AdjustOfferPriceAmt = null,
    decimal? SellingPrice = null,
    string? SellingPriceUnit = null,
    int? BuySellYear = null,
    int? BuySellMonth = null,
    decimal? AdjustedPeriodPct = null,
    decimal? CumulativeAdjPeriod = null,
    decimal? LandAreaDeficient = null,
    string? LandAreaDeficientUnit = null,
    decimal? LandPrice = null,
    decimal? LandValueAdjustment = null,
    decimal? UsableAreaDeficient = null,
    string? UsableAreaDeficientUnit = null,
    decimal? UsableAreaPrice = null,
    decimal? BuildingValueAdjustment = null,
    decimal? TotalFactorDiffPct = null,
    decimal? TotalFactorDiffAmt = null,
    decimal? TotalAdjustedValue = null,
    decimal? Weight = null,
    decimal? WeightedAdjustedValue = null
);
