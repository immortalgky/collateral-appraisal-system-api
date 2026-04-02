using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetMachineCostItems;

public record GetMachineCostItemsQuery(
    Guid PricingAnalysisId,
    Guid MethodId
) : IQuery<GetMachineCostItemsResult>;
