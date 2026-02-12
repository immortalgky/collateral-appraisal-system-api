namespace Appraisal.Application.Features.PricingAnalysis.GetComparativeFactors;

public record GetComparativeFactorsResponse(
    Guid PricingAnalysisId,
    Guid MethodId,
    string MethodType,
    IReadOnlyList<LinkedComparableDto> LinkedComparables,
    IReadOnlyList<ComparativeFactorDto> ComparativeFactors,
    IReadOnlyList<FactorScoreDto> FactorScores,
    IReadOnlyList<CalculationDto> Calculations
);
