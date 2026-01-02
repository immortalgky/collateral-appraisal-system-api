using Auth.Services;

namespace Auth.Domain.Roles.Features.DeleteRole;

public class DeleteRoleCommandHandler(IRoleService roleService)
    : ICommandHandler<DeleteRoleCommand, DeleteRoleResult>
{
    public async Task<DeleteRoleResult> Handle(
        DeleteRoleCommand command,
        CancellationToken cancellationToken
    )
    {
        await roleService.DeleteRole(command.Id, cancellationToken);
        return new DeleteRoleResult(true);
    }
}
