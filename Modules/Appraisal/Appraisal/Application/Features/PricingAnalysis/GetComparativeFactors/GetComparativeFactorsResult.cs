namespace Appraisal.Application.Features.PricingAnalysis.GetComparativeFactors;

public record GetComparativeFactorsResult(
    Guid PricingAnalysisId,
    Guid MethodId,
    string MethodType,
    IReadOnlyList<LinkedComparableDto> LinkedComparables,
    IReadOnlyList<ComparativeFactorDto> ComparativeFactors,
    IReadOnlyList<FactorScoreDto> FactorScores,
    IReadOnlyList<CalculationDto> Calculations
);

/// <summary>
/// Linked comparable info for display
/// </summary>
public record LinkedComparableDto(
    Guid LinkId,
    Guid MarketComparableId,
    int DisplaySequence,
    string? ComparableName,
    string? ComparableCode
);

/// <summary>
/// Step 1 factor selection data
/// </summary>
public record ComparativeFactorDto(
    Guid Id,
    Guid FactorId,
    string? FactorName,
    string? FactorCode,
    int DisplaySequence,
    bool IsSelectedForScoring,
    string? Remarks
);

/// <summary>
/// Step 2 factor score data
/// </summary>
public record FactorScoreDto(
    Guid Id,
    Guid FactorId,
    string? FactorName,
    Guid? MarketComparableId,
    string? ComparableName,
    decimal FactorWeight,
    int DisplaySequence,
    string? Value,
    decimal? Score,
    decimal? WeightedScore,
    decimal? AdjustmentPct,
    string? Remarks
);

/// <summary>
/// Calculation data per comparable
/// </summary>
public record CalculationDto(
    Guid Id,
    Guid MarketComparableId,
    string? ComparableName,
    decimal? OfferingPrice,
    string? OfferingPriceUnit,
    decimal? AdjustOfferPricePct,
    decimal? SellingPrice,
    int? BuySellYear,
    int? BuySellMonth,
    decimal? AdjustedPeriodPct,
    decimal? CumulativeAdjPeriod,
    decimal? TotalFactorDiffPct,
    decimal? TotalAdjustedValue
);
