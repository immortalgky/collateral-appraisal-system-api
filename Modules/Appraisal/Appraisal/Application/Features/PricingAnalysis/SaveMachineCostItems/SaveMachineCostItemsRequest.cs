namespace Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;

public record SaveMachineCostItemsRequest(
    IReadOnlyList<MachineCostItemInput> Items,
    string? Remark = null
);

/// <summary>
/// Id = null for new items, existing Id for updates.
/// Items not in the request but existing in DB will be removed.
/// </summary>
public record MachineCostItemInput(
    Guid? Id,
    Guid AppraisalPropertyId,
    int DisplaySequence,
    decimal? RcnReplacementCost,
    decimal? LifeSpanYears,
    decimal ConditionFactor,
    decimal FunctionalObsolescence,
    decimal EconomicObsolescence,
    decimal? FairMarketValue,
    bool MarketDemandAvailable,
    string? Notes
);
