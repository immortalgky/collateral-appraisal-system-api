using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetIncomeAnalysis;

public record GetIncomeAnalysisQuery(
    Guid PricingAnalysisId,
    Guid MethodId
) : IQuery<GetIncomeAnalysisResult>;
