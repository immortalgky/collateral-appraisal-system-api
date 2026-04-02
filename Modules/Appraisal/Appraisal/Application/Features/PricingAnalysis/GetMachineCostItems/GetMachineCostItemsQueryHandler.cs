using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetMachineCostItems;

public class GetMachineCostItemsQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetMachineCostItemsQuery, GetMachineCostItemsResult>
{
    public async Task<GetMachineCostItemsResult> Handle(
        GetMachineCostItemsQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(query.PricingAnalysisId, cancellationToken)
                              ?? throw new InvalidOperationException(
                                  $"Pricing analysis {query.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == query.MethodId)
            ?? throw new InvalidOperationException($"Method {query.MethodId} not found");

        var items = method.MachineCostItems
            .OrderBy(i => i.DisplaySequence)
            .Select(i => new MachineCostItemDto(
                i.Id,
                i.AppraisalPropertyId,
                i.DisplaySequence,
                i.RcnReplacementCost,
                i.LifeSpanYears,
                i.ConditionFactor,
                i.FunctionalObsolescence,
                i.EconomicObsolescence,
                i.FairMarketValue,
                i.MarketDemandAvailable,
                i.Notes
            ))
            .ToList();

        var totalFmv = items.Sum(i => i.FairMarketValue ?? 0);

        return new GetMachineCostItemsResult(items, totalFmv, method.Remark);
    }
}
