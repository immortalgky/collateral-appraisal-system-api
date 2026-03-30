namespace Auth.Application.Features.Roles.UpdateRolePermissions;

public record UpdateRolePermissionsRequest(List<Guid> PermissionIds);
