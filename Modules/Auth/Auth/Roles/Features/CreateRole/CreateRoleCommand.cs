namespace Auth.Roles.Features.CreateRole;

public record CreateRoleCommand(string Name, string Description) : ICommand<CreateRoleResult>;
