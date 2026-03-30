using Auth.Services;

namespace Auth.Application.Features.Roles.UpdateRolePermissions;

public class UpdateRolePermissionsCommandHandler(IRoleService roleService)
    : ICommandHandler<UpdateRolePermissionsCommand, UpdateRolePermissionsResult>
{
    public async Task<UpdateRolePermissionsResult> Handle(
        UpdateRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        await roleService.UpdateRolePermissions(command.RoleId, command.PermissionIds, cancellationToken);
        return new UpdateRolePermissionsResult(true);
    }
}
