namespace Auth.Roles.Features.CreateRole;

public record CreateRoleRequest(string Name, string Description, List<Guid> Permissions);
