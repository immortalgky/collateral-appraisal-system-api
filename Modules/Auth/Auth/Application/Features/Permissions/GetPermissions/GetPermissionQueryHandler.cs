using Auth.Services;
using Shared.Pagination;

namespace Auth.Application.Features.Permissions.GetPermissions;

public class GetPermissionQueryHandler(IPermissionService permissionService)
    : IQueryHandler<GetPermissionQuery, GetPermissionResult>
{
    public async Task<GetPermissionResult> Handle(
        GetPermissionQuery query,
        CancellationToken cancellationToken)
    {
        var paginationRequest = new PaginationRequest(query.PageNumber - 1, query.PageSize);
        var paginated = await permissionService.GetPermissions(query.Search, paginationRequest, cancellationToken);

        var items = paginated.Items.Select(p => new PermissionItemDto(p.Id, p.PermissionCode, p.DisplayName, p.Description, p.Module));

        return new GetPermissionResult(items, paginated.Count, paginated.PageNumber, paginated.PageSize);
    }
}
