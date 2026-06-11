using Auth.Infrastructure;
using Auth.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Pagination;

namespace Auth.Application.Features.Roles.GetRoles;

public class GetRoleQueryHandler(IRoleService roleService, AuthDbContext dbContext)
    : IQueryHandler<GetRoleQuery, GetRoleResult>
{
    public async Task<GetRoleResult> Handle(GetRoleQuery query, CancellationToken cancellationToken)
    {
        var paginationRequest = new PaginationRequest(query.PageNumber - 1, query.PageSize);
        var paginated = await roleService.GetRoles(query.Search, query.Scope, paginationRequest, cancellationToken);

        // Count users assigned to each role on this page (one grouped query).
        var roleIds = paginated.Items.Select(r => r.Id).ToList();
        var userCounts = await dbContext.UserRoles
            .Where(ur => roleIds.Contains(ur.RoleId))
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var items = paginated.Items.Select(r => new RoleListItemDto(
            r.Id, r.Name ?? "", r.Description, r.Scope, r.Permissions.Count,
            userCounts.TryGetValue(r.Id, out var c) ? c : 0));

        return new GetRoleResult(items, paginated.Count, paginated.PageNumber, paginated.PageSize);
    }
}
