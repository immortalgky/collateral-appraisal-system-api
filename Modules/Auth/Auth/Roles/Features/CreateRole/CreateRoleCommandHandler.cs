using Auth.Services;

namespace Auth.Roles.Features.CreateRole;

public class CreateRoleCommandHandler(IRoleService roleService)
    : ICommandHandler<CreateRoleCommand, CreateRoleResult>
{
    public async Task<CreateRoleResult> Handle(
        CreateRoleCommand command,
        CancellationToken cancellationToken
    )
    {
        var roleDto = command.Adapt<RoleDto>();
        var createdRole = await roleService.CreateRole(roleDto, cancellationToken);
        return new CreateRoleResult(createdRole.Id);
    }
}
