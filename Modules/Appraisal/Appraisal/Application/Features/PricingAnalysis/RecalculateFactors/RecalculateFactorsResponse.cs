namespace Appraisal.Application.Features.PricingAnalysis.RecalculateFactors;

public record RecalculateFactorsResponse(
    Guid PricingCalculationId,
    decimal? TotalFactorDiffPct
);
