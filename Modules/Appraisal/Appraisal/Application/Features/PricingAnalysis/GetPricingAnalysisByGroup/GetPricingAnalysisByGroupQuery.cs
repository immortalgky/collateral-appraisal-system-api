using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisByGroup;

/// <summary>
/// Query to get pricing analysis by property group ID
/// </summary>
public record GetPricingAnalysisByGroupQuery(
    Guid PropertyGroupId
) : IQuery<GetPricingAnalysisByGroupResult>;
