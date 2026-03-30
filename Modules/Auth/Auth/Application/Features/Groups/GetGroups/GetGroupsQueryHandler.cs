using Auth.Application.Services;
using Shared.Pagination;

namespace Auth.Application.Features.Groups.GetGroups;

public class GetGroupsQueryHandler(IGroupService groupService)
    : IQueryHandler<GetGroupsQuery, GetGroupsResult>
{
    public async Task<GetGroupsResult> Handle(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        var paginationRequest = new PaginationRequest(query.PageNumber - 1, query.PageSize);
        var paginated = await groupService.GetGroups(query.Search, query.Scope, paginationRequest, cancellationToken);

        var items = paginated.Items.Select(g => new GroupListItemDto(
            g.Id, g.Name, g.Description, g.Scope, g.CompanyId, g.Users.Count));

        return new GetGroupsResult(items, paginated.Count, paginated.PageNumber, paginated.PageSize);
    }
}
