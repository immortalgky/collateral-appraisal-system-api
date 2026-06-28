namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public record SaveHypothesisAnalysisRequest(
    LandBuildingSummaryInput? LandBuildingSummary,
    CondominiumSummaryInput? CondominiumSummary,
    IReadOnlyList<HypothesisCostItemInput> CostItems,
    string? Remark = null,
    // User-overridden adjusted final value (stored as-is; never recomputed)
    decimal? FinalValueAdjusted = null,
    // User-rounded appraisal price override
    decimal? AppraisalPrice = null
);
