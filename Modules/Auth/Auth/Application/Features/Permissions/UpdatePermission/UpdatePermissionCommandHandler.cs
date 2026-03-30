using Auth.Services;

namespace Auth.Application.Features.Permissions.UpdatePermission;

public class UpdatePermissionCommandHandler(IPermissionService permissionService)
    : ICommandHandler<UpdatePermissionCommand, UpdatePermissionResult>
{
    public async Task<UpdatePermissionResult> Handle(
        UpdatePermissionCommand command,
        CancellationToken cancellationToken)
    {
        await permissionService.UpdatePermission(
            command.Id,
            command.DisplayName,
            command.Description,
            command.Module,
            cancellationToken);

        return new UpdatePermissionResult(true);
    }
}
