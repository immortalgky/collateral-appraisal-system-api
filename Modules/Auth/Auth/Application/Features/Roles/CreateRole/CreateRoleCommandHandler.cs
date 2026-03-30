using Auth.Services;

namespace Auth.Application.Features.Roles.CreateRole;

public class CreateRoleCommandHandler(IRoleService roleService)
    : ICommandHandler<CreateRoleCommand, CreateRoleResult>
{
    public async Task<CreateRoleResult> Handle(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        var role = await roleService.CreateRole(
            command.Name,
            command.Description,
            command.Scope,
            command.PermissionIds,
            cancellationToken);

        return new CreateRoleResult(role.Id);
    }
}
