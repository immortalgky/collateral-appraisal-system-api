namespace Auth.Application.Features.Roles.UpdateRolePermissions;

public record UpdateRolePermissionsCommand(Guid RoleId, List<Guid> PermissionIds)
    : ICommand<UpdateRolePermissionsResult>;
