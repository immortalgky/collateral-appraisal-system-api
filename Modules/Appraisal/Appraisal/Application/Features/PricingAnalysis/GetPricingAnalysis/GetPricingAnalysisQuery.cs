using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

/// <summary>
/// Query to get a pricing analysis by ID
/// </summary>
public record GetPricingAnalysisQuery(
    Guid Id
) : IQuery<GetPricingAnalysisResult>;
