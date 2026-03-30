using Auth.Services;
using Shared.Exceptions;

namespace Auth.Application.Features.Permissions.GetPermissionById;

public class GetPermissionByIdQueryHandler(IPermissionService permissionService)
    : IQueryHandler<GetPermissionByIdQuery, GetPermissionByIdResult>
{
    public async Task<GetPermissionByIdResult> Handle(
        GetPermissionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var permission = await permissionService.GetPermissionById(query.Id, cancellationToken)
            ?? throw new NotFoundException("Permission", query.Id);

        return new GetPermissionByIdResult(permission.Id, permission.PermissionCode, permission.DisplayName, permission.Description, permission.Module);
    }
}
