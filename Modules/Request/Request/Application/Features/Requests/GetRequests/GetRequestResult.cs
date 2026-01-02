using Shared.Pagination;

namespace Request.Application.Features.Requests.GetRequests;

public record GetRequestResult(PaginatedResult<RequestDto> Result);