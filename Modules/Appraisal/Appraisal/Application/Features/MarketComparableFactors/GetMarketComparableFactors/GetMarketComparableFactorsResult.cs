namespace Appraisal.Application.Features.MarketComparableFactors.GetMarketComparableFactors;

/// <summary>
/// Result containing all market comparable factors.
/// </summary>
public sealed record GetMarketComparableFactorsResult(
    IReadOnlyList<MarketComparableFactorDto> Factors);
