using Auth.Services;

namespace Auth.Permissions.Features.GetPermissions;

public class GetPermissionQueryHandler(IPermissionService permissionService)
    : IQueryHandler<GetPermissionQuery, GetPermissionResult>
{
    public async Task<GetPermissionResult> Handle(
        GetPermissionQuery query,
        CancellationToken cancellationToken
    )
    {
        var pagination = await permissionService.GetPermissions(
            query.PaginationRequest,
            cancellationToken
        );
        var paginationDto = new PaginatedResult<PermissionDto>(
            pagination.Items.Select(item => item.ToDto()),
            pagination.Count,
            pagination.PageNumber,
            pagination.PageSize
        );

        return new GetPermissionResult(paginationDto);
    }
}
