using Auth.Services;

namespace Auth.Permissions.Features.DeletePermission;

public class DeletePermissionCommandHandler(IPermissionService permissionService)
    : ICommandHandler<DeletePermissionCommand, DeletePermissionResult>
{
    public async Task<DeletePermissionResult> Handle(
        DeletePermissionCommand command,
        CancellationToken cancellationToken
    )
    {
        await permissionService.DeletePermission(command.Id, cancellationToken);
        return new DeletePermissionResult(true);
    }
}
