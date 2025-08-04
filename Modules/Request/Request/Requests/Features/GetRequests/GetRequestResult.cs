using Shared.Pagination;

namespace Request.Requests.Features.GetRequests;

public record GetRequestResult(PaginatedResult<RequestDto> Result);