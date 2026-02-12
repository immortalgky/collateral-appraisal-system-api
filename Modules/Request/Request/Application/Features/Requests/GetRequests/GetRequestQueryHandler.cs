using Shared.Pagination;

namespace Request.Application.Features.Requests.GetRequests;

internal class GetRequestQueryHandler(RequestDbContext dbContext)
    : IQueryHandler<GetRequestQuery, GetRequestResult>
{
    public async Task<GetRequestResult> Handle(GetRequestQuery query, CancellationToken cancellationToken)
    {
        var queryable = dbContext.Requests
            .AsNoTracking()
            .Select(r => new GetRequestListItem
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber!.Value ?? "",
                Status = r.Status.Code,
                Purpose = r.Purpose,
                Channel = r.Channel,
                Priority = r.Priority
            });

        var paginatedResult = await queryable.ToPaginatedResultAsync(
            query.PaginationRequest,
            cancellationToken);

        return new GetRequestResult(paginatedResult);
    }
}