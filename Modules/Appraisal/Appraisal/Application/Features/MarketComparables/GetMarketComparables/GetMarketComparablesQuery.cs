using Shared.CQRS;
using Shared.Pagination;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparables;

/// <summary>
/// Query to get all Market Comparables with pagination
/// </summary>
public record GetMarketComparablesQuery(PaginationRequest PaginationRequest) : IQuery<GetMarketComparablesResult>;