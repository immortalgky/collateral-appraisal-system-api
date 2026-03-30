namespace Auth.Application.Features.Roles.CreateRole;

public record CreateRoleCommand(string Name, string Description, string? Scope, List<Guid> PermissionIds)
    : ICommand<CreateRoleResult>;
