namespace Auth.Application.Features.Roles.DeleteRole;

public record DeleteRoleCommand(Guid Id) : ICommand<DeleteRoleResult>;
