namespace Appraisal.Application.Features.PricingAnalysis.RecalculateFactors;

/// <summary>
/// Result of recalculating total factor adjustment
/// </summary>
public record RecalculateFactorsResult(
    Guid PricingCalculationId,
    decimal? TotalFactorDiffPct
);
