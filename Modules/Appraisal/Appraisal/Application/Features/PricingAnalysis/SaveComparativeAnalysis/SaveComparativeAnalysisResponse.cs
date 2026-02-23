namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

public record SaveComparativeAnalysisResponse(
    Guid PricingAnalysisId,
    Guid MethodId,
    int ComparativeFactorsCount,
    int FactorScoresCount,
    int CalculationsCount,
    bool Success
);
