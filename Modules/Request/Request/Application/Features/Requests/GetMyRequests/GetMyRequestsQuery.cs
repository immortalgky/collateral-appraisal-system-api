using Shared.Pagination;

namespace Request.Application.Features.Requests.GetMyRequests;

public record GetMyRequestsQuery(
    PaginationRequest PaginationRequest,
    string? Search = null,
    string? Status = null,
    string? Purpose = null,
    string? SortBy = null,
    string? SortDirection = null
) : IQuery<GetMyRequestsResult>;
