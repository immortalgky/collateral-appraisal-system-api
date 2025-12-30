using Auth.Services;

namespace Auth.Domain.Roles.Features.CreateRole;

public class CreateRoleCommandHandler(IRoleService roleService)
    : ICommandHandler<CreateRoleCommand, CreateRoleResult>
{
    public async Task<CreateRoleResult> Handle(
        CreateRoleCommand command,
        CancellationToken cancellationToken
    )
    {
        var roleDto = command.Adapt<CreateRoleDto>();
        var createdRole = await roleService.CreateRole(roleDto, cancellationToken);
        return new CreateRoleResult(createdRole.Id);
    }
}
