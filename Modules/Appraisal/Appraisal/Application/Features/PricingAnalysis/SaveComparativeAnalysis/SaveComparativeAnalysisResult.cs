namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

public record SaveComparativeAnalysisResult(
    Guid PricingAnalysisId,
    Guid MethodId,
    int ComparativeFactorsCount,
    int FactorScoresCount,
    int CalculationsCount,
    bool Success
);
