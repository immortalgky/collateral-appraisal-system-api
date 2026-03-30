namespace Auth.Application.Features.Roles.UpdateRole;

public record UpdateRoleCommand(Guid Id, string Name, string Description, string? Scope)
    : ICommand<UpdateRoleResult>;
