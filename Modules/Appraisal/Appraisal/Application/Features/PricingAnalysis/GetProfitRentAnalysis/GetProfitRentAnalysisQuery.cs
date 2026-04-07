using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetProfitRentAnalysis;

public record GetProfitRentAnalysisQuery(
    Guid PricingAnalysisId,
    Guid MethodId
) : IQuery<GetProfitRentAnalysisResult>;
