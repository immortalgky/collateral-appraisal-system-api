using Auth.Services;

namespace Auth.Application.Features.Roles.UpdateRoleUsers;

public class UpdateRoleUsersCommandHandler(IRoleService roleService)
    : ICommandHandler<UpdateRoleUsersCommand, UpdateRoleUsersResult>
{
    public async Task<UpdateRoleUsersResult> Handle(UpdateRoleUsersCommand command, CancellationToken cancellationToken)
    {
        await roleService.UpdateRoleUsers(command.RoleId, command.UserIds, cancellationToken);
        return new UpdateRoleUsersResult(true);
    }
}
