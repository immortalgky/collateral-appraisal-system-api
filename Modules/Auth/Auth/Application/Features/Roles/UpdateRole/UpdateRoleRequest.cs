namespace Auth.Application.Features.Roles.UpdateRole;

public record UpdateRoleRequest(string Name, string Description, string? Scope);
