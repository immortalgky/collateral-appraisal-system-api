using Shared.Pagination;

namespace Request.Requests.Features.GetRequests;

public record GetRequestQuery(PaginationRequest PaginationRequest) : IQuery<GetRequestResult>;