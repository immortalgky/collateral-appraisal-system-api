using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;

public record GetHypothesisAnalysisQuery(
    Guid PricingAnalysisId,
    Guid MethodId
) : IQuery<GetHypothesisAnalysisResult>;
