namespace Appraisal.Application.Features.PricingAnalysis.GetMachineCostItems;

public record GetMachineCostItemsResult(
    List<MachineCostItemDto> Items,
    decimal TotalFmv,
    string? Remark
);

public record MachineCostItemDto(
    Guid Id,
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
