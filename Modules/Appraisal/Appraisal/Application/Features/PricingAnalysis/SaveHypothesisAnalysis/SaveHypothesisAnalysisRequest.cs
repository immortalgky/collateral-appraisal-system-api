namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public record SaveHypothesisAnalysisRequest(
    LandBuildingSummaryInput? LandBuildingSummary,
    CondominiumSummaryInput? CondominiumSummary,
    IReadOnlyList<HypothesisCostItemInput> CostItems,
    string? Remark = null,
    // User-overridden adjusted final value (stored as-is; never recomputed)
    decimal? FinalValueOverride = null,
    // User-rounded appraisal price override
    decimal? IndicatedValue = null,
    // L&B house model → building. Null = leave the saved mappings as they are (older clients);
    // an empty list clears them.
    IReadOnlyList<ModelBuildingMappingInput>? ModelBuildingMappings = null
);
