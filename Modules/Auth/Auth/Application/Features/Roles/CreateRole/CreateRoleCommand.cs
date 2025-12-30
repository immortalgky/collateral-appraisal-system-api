namespace Auth.Domain.Roles.Features.CreateRole;

public record CreateRoleCommand(string Name, string Description, List<Guid> Permissions)
    : ICommand<CreateRoleResult>;
