using Request.Application.Features.Requests.GetRequests;
using Shared.Identity;
using Shared.Pagination;

namespace Request.Application.Features.Requests.GetMyRequests;

internal class GetMyRequestsQueryHandler(RequestDbContext dbContext, ICurrentUserService currentUserService)
    : IQueryHandler<GetMyRequestsQuery, GetMyRequestsResult>
{
    public async Task<GetMyRequestsResult> Handle(GetMyRequestsQuery query, CancellationToken cancellationToken)
    {
        var queryable = dbContext.Requests.AsNoTracking().AsQueryable();

        // Filter by current user
        var username = currentUserService.Username;
        queryable = queryable.Where(r => r.Requestor.UserId == username);

        // Filter by status (default: Draft or New)
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = RequestStatus.FromString(query.Status.ToUpperInvariant());
            queryable = queryable.Where(r => r.Status == status);
        }
        else
        {
            queryable = queryable.Where(r => r.Status == RequestStatus.Draft || r.Status == RequestStatus.New);
        }

        // Filter by purpose
        if (!string.IsNullOrWhiteSpace(query.Purpose)) queryable = queryable.Where(r => r.Purpose == query.Purpose);

        // Search by request number or customer name
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            queryable = queryable.Where(r =>
                r.RequestNumber!.Value.Contains(search) ||
                r.Customers.Any(c => c.Name.Contains(search)));
        }

        // Sort (before projection so EF can translate entity properties)
        var isDescending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "requestnumber" => isDescending
                ? queryable.OrderByDescending(r => r.RequestNumber!.Value)
                : queryable.OrderBy(r => r.RequestNumber!.Value),
            "status" => isDescending
                ? queryable.OrderByDescending(r => r.Status)
                : queryable.OrderBy(r => r.Status),
            "purpose" => isDescending
                ? queryable.OrderByDescending(r => r.Purpose)
                : queryable.OrderBy(r => r.Purpose),
            "customername" => isDescending
                ? queryable.OrderByDescending(r => r.Customers.Select(c => c.Name).FirstOrDefault())
                : queryable.OrderBy(r => r.Customers.Select(c => c.Name).FirstOrDefault()),
            "createdat" => isDescending
                ? queryable.OrderByDescending(r => r.CreatedAt)
                : queryable.OrderBy(r => r.CreatedAt),
            _ => queryable.OrderByDescending(r => r.CreatedAt)
        };

        // Project to DTO
        var projected = ordered.Select(r => new GetRequestListItem
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber!.Value ?? "",
            Status = r.Status.Code,
            Purpose = r.Purpose,
            Channel = r.Channel,
            Priority = r.Priority,
            CustomerName = r.Customers.Select(c => c.Name).FirstOrDefault(),
            ContactNumber = r.Customers.Select(c => c.ContactNumber).FirstOrDefault(),
            CreatedAt = r.CreatedAt
        });

        var paginatedResult = await projected.ToPaginatedResultAsync(
            query.PaginationRequest,
            cancellationToken);

        return new GetMyRequestsResult(paginatedResult);
    }
}