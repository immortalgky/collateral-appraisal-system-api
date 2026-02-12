using Shared.Pagination;

namespace Request.Application.Features.Requests.GetRequests;

public record GetRequestsResponse(PaginatedResult<GetRequestListItem> Result);
