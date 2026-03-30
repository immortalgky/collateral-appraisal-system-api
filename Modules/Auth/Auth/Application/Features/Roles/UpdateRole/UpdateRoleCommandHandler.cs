using Auth.Services;

namespace Auth.Application.Features.Roles.UpdateRole;

public class UpdateRoleCommandHandler(IRoleService roleService)
    : ICommandHandler<UpdateRoleCommand, UpdateRoleResult>
{
    public async Task<UpdateRoleResult> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        await roleService.UpdateRole(command.Id, command.Name, command.Description, command.Scope, cancellationToken);
        return new UpdateRoleResult(true);
    }
}
