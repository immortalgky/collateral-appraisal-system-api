using Auth.Services;

namespace Auth.Domain.Permissions.Features.GetPermissionById;

public class GetPermissionByIdQueryHandler(IPermissionService permissionService)
    : IQueryHandler<GetPermissionByIdQuery, GetPermissionByIdResult>
{
    public async Task<GetPermissionByIdResult> Handle(
        GetPermissionByIdQuery query,
        CancellationToken cancellationToken
    )
    {
        var permission = await permissionService.GetPermissionById(query.Id, cancellationToken);
        return permission.Adapt<GetPermissionByIdResult>();
    }
}
