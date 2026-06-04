namespace Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;

public record CreateOrGetReferenceResponse(
    Guid PricingAnalysisId,
    Guid MarketApproachId,
    bool WasCreated
);
