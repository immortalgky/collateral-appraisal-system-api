using Request.Application.Features.Requests.GetRequests;
using Shared.Pagination;

namespace Request.Application.Features.Requests.GetMyRequests;

public record GetMyRequestsResult(PaginatedResult<GetRequestListItem> Result);
