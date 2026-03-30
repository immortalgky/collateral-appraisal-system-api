namespace Auth.Application.Features.Roles.CreateRole;

public record CreateRoleRequest(string Name, string Description, string? Scope, List<Guid> PermissionIds);
