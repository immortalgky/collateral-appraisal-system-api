using Shared.Pagination;

namespace Request.Application.Features.Requests.GetRequests;

public record GetRequestQuery(
    PaginationRequest PaginationRequest,
    string? Search = null,
    string? Status = null,
    string? Purpose = null,
    string? SortBy = null,
    string? SortDirection = null
) : IQuery<GetRequestResult>;