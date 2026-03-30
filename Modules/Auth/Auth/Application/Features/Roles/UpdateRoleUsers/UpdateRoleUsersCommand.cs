namespace Auth.Application.Features.Roles.UpdateRoleUsers;

public record UpdateRoleUsersCommand(Guid RoleId, List<Guid> UserIds)
    : ICommand<UpdateRoleUsersResult>;
