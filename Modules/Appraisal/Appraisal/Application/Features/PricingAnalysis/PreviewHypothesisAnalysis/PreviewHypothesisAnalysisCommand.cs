using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

/// <summary>
/// Returns a full computed snapshot without persisting anything.
/// Used by the frontend for live preview while the user types.
/// </summary>
public record PreviewHypothesisAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    SaveHypothesisAnalysis.LandBuildingSummaryInput? LandBuildingSummary,
    SaveHypothesisAnalysis.CondominiumSummaryInput? CondominiumSummary,
    IReadOnlyList<SaveHypothesisAnalysis.HypothesisCostItemInput> CostItems
) : IQuery<PreviewHypothesisAnalysisResult>;
