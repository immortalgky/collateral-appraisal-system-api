using Auth.Services;

namespace Auth.Application.Features.Roles.GetRoleUsers;

public class GetRoleUsersQueryHandler(IRoleService roleService)
    : IQueryHandler<GetRoleUsersQuery, GetRoleUsersResult>
{
    public async Task<GetRoleUsersResult> Handle(GetRoleUsersQuery query, CancellationToken cancellationToken)
    {
        var users = await roleService.GetRoleUsers(query.RoleId, cancellationToken);

        var dtos = users.Select(u => new RoleUserDto(
            u.Id, u.UserName ?? "", u.FirstName, u.LastName, u.Email)).ToList();

        return new GetRoleUsersResult(dtos);
    }
}
