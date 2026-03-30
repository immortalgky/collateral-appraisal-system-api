using Auth.Services;
using Shared.Exceptions;

namespace Auth.Application.Features.Roles.GetRoleById;

public class GetRoleByIdQueryHandler(IRoleService roleService)
    : IQueryHandler<GetRoleByIdQuery, GetRoleByIdResult>
{
    public async Task<GetRoleByIdResult> Handle(
        GetRoleByIdQuery query,
        CancellationToken cancellationToken)
    {
        var role = await roleService.GetRoleById(query.Id, cancellationToken)
            ?? throw new NotFoundException("Role", query.Id);

        var permissions = role.Permissions
            .Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.PermissionCode, rp.Permission.Description))
            .ToList();

        return new GetRoleByIdResult(role.Id, role.Name ?? "", role.Description, role.Scope, permissions);
    }
}
