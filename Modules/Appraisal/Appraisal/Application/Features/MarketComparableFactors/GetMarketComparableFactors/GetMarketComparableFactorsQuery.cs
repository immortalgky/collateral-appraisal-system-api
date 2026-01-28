using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.GetMarketComparableFactors;

/// <summary>
/// Query to retrieve all market comparable factors.
/// </summary>
public sealed record GetMarketComparableFactorsQuery(
    bool ActiveOnly = true) : IQuery<GetMarketComparableFactorsResult>;
