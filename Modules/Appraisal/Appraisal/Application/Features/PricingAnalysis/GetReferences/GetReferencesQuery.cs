using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetReferences;

/// <summary>
/// Query to list reference PricingAnalyses for a given anchor, returning a summary of
/// each analysis' methods (methodId, methodType, finalValue, valuePerUnit).
/// </summary>
public record GetReferencesQuery(
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    string? AnchorRefKey = null
) : IQuery<GetReferencesResult>;
