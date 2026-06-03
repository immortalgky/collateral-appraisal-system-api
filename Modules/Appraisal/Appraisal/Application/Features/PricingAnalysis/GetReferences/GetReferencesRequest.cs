namespace Appraisal.Application.Features.PricingAnalysis.GetReferences;

/// <summary>Query-string parameters for the GetReferences endpoint.</summary>
public record GetReferencesRequest(
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    string? AnchorRefKey = null
);
