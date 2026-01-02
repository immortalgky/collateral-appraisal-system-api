using Auth.Services;

namespace Auth.Domain.Permissions.Features.CreatePermission;

public class CreatePermissionCommandHandler(IPermissionService permissionService)
    : ICommandHandler<CreatePermissionCommand, CreatePermissionResult>
{
    public async Task<CreatePermissionResult> Handle(
        CreatePermissionCommand command,
        CancellationToken cancellationToken
    )
    {
        var permissionDto = command.Adapt<PermissionDto>();
        var createdPermission = await permissionService.CreatePermission(
            permissionDto,
            cancellationToken
        );
        return new CreatePermissionResult(createdPermission.Id);
    }
}
