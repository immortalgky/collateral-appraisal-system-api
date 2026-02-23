using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparableById;

/// <summary>
/// Query to get a market comparable by ID with full details including factor data and images
/// </summary>
public record GetMarketComparableByIdQuery(Guid Id) : IQuery<GetMarketComparableByIdResult>;
