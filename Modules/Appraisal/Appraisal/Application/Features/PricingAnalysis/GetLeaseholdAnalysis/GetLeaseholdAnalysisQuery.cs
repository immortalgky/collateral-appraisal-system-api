using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetLeaseholdAnalysis;

public record GetLeaseholdAnalysisQuery(
    Guid PricingAnalysisId,
    Guid MethodId
) : IQuery<GetLeaseholdAnalysisResult>;
