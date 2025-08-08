using Shared.Pagination;

namespace Request.Requests.Features.GetRequests;

public class GetRequestHandler(RequestDbContext dbContext, IRequestReadRepository repository)
    : IQueryHandler<GetRequestQuery, GetRequestResult>
{
    public async Task<GetRequestResult> Handle(GetRequestQuery query, CancellationToken cancellationToken)
    {
        var requests = await repository.GetPaginatedAsync(query.PaginationRequest, cancellationToken);

        var result = requests.Adapt<PaginatedResult<RequestDto>>();

        return new GetRequestResult(result);
    }
}