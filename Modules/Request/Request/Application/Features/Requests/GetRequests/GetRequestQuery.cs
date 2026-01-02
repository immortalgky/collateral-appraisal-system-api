using Shared.Pagination;

namespace Request.Application.Features.Requests.GetRequests;

public record GetRequestQuery(PaginationRequest PaginationRequest) : IQuery<GetRequestResult>;