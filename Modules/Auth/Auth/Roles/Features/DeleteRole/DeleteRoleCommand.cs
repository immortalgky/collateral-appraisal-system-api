namespace Auth.Roles.Features.DeleteRole;

public record DeleteRoleCommand(Guid Id) : ICommand<DeleteRoleResult>;
