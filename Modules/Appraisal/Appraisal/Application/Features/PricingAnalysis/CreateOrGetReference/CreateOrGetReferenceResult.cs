namespace Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;

/// <summary>
/// Result of the find-or-create reference operation.
/// </summary>
public record CreateOrGetReferenceResult(
    Guid PricingAnalysisId,
    Guid MarketApproachId,
    bool WasCreated
);
