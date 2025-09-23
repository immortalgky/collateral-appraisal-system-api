using Auth.Services;

namespace Auth.Roles.Features.GetRoleById;

public class GetRoleByIdQueryHandler(IRoleService roleService)
    : IQueryHandler<GetRoleByIdQuery, GetRoleByIdResult>
{
    public async Task<GetRoleByIdResult> Handle(
        GetRoleByIdQuery query,
        CancellationToken cancellationToken
    )
    {
        var role = await roleService.GetRoleById(query.Id, cancellationToken);
        return role.Adapt<GetRoleByIdResult>();
    }
}
