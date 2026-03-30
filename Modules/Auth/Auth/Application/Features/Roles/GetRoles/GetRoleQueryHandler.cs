using Auth.Services;
using Shared.Pagination;

namespace Auth.Application.Features.Roles.GetRoles;

public class GetRoleQueryHandler(IRoleService roleService)
    : IQueryHandler<GetRoleQuery, GetRoleResult>
{
    public async Task<GetRoleResult> Handle(GetRoleQuery query, CancellationToken cancellationToken)
    {
        var paginationRequest = new PaginationRequest(query.PageNumber - 1, query.PageSize);
        var paginated = await roleService.GetRoles(query.Search, query.Scope, paginationRequest, cancellationToken);

        var items = paginated.Items.Select(r => new RoleListItemDto(
            r.Id, r.Name ?? "", r.Description, r.Scope, r.Permissions.Count));

        return new GetRoleResult(items, paginated.Count, paginated.PageNumber, paginated.PageSize);
    }
}
