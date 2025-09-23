using Auth.Services;

namespace Auth.Permissions.Features.CreatePermission;

public class CreatePermissionCommandHandler(IPermissionService permissionService)
    : ICommandHandler<CreatePermissionCommand, CreatePermissionResult>
{
    public async Task<CreatePermissionResult> Handle(
        CreatePermissionCommand command,
        CancellationToken cancellationToken
    )
    {
        var permissionDto = command.Adapt<PermissionDto>();
        await permissionService.CreatePermission(permissionDto, cancellationToken);
        return new CreatePermissionResult(true);
    }
}
