using Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;
using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Services;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

/// <summary>
/// Snapshot returned by the preview endpoint.
/// <see cref="CostItems"/> carries the server-computed B03/B06/B07/B08 fields for CostOfBuilding rows
/// so the FE can render them without a round-trip save.
/// <see cref="Models"/> carries B09/B10/B11 per-model totals.
/// <see cref="TotalLandAreaFromTitles"/> is the system-derived C01 (sum of land title areas for the
/// property group). Null when the analysis is on a ProjectModel or the group has no titles.
/// </summary>
public record PreviewHypothesisAnalysisResult(
    HypothesisVariant Variant,
    LandBuildingSummary? LandBuildingSummary,
    Dictionary<string, HypothesisCalculationService.LandBuildingModelAggregate>? Models,
    CondominiumSummary? CondominiumSummary,
    IReadOnlyList<CostItemDto>? CostItems,
    decimal? TotalLandAreaFromTitles = null
);
