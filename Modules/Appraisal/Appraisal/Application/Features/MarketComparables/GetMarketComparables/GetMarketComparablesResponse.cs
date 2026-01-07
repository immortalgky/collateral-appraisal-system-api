using Shared.Pagination;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparables;

public record GetMarketComparablesResponse(PaginatedResult<MarketComparableDto> Result);