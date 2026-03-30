namespace Auth.Application.Features.Users.UpdateUserRoles;

public record UpdateUserRolesCommand(Guid UserId, List<string> RoleNames) : ICommand;
