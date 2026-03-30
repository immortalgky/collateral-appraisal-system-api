namespace Auth.Application.Features.Roles.GetRoleUsers;

public record GetRoleUsersQuery(Guid RoleId) : IQuery<GetRoleUsersResult>;
