namespace Appraisal.Application.Features.PricingAnalysis.CreateReferenceFromMethod;

/// <summary>
/// HTTP request body for POST /pricing-analysis/references/from-method.
/// </summary>
public record CreateReferenceFromMethodRequest(
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    Guid? HostMethodId,
    Guid SourcePricingAnalysisId,
    Guid SourceMethodId,
    decimal? LandAreaOverride = null
);
