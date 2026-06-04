namespace Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;

/// <summary>
/// HTTP request body for the CreateOrGetReference endpoint.
/// </summary>
public record CreateOrGetReferenceRequest(
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    string? AnchorRefKey = null,
    Guid? HostMethodId = null
);
