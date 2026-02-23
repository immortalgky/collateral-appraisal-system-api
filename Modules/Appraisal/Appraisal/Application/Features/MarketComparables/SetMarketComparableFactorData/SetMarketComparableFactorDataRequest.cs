namespace Appraisal.Application.Features.MarketComparables.SetMarketComparableFactorData;

/// <summary>
/// Request to set factor data for a market comparable
/// </summary>
public record SetMarketComparableFactorDataRequest(
    List<FactorDataItem> FactorData
);

/// <summary>
/// Individual factor data item
/// </summary>
public record FactorDataItem(
    Guid FactorId,
    string? Value,
    string? OtherRemarks = null
);
