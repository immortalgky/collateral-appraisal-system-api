namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public record SaveHypothesisAnalysisRequest(
    LandBuildingSummaryInput? LandBuildingSummary,
    CondominiumSummaryInput? CondominiumSummary,
    IReadOnlyList<HypothesisCostItemInput> CostItems,
    string? Remark = null
);
