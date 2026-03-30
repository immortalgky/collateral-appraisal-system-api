using Auth.Services;

namespace Auth.Application.Features.Permissions.CreatePermission;

public class CreatePermissionCommandHandler(IPermissionService permissionService)
    : ICommandHandler<CreatePermissionCommand, CreatePermissionResult>
{
    public async Task<CreatePermissionResult> Handle(
        CreatePermissionCommand command,
        CancellationToken cancellationToken)
    {
        var permission = await permissionService.CreatePermission(
            command.PermissionCode,
            command.DisplayName,
            command.Description,
            command.Module,
            cancellationToken);

        return new CreatePermissionResult(permission.Id);
    }
}
