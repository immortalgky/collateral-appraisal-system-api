namespace Auth.Application.Features.Permissions.CreatePermission;

public record CreatePermissionRequest(string PermissionCode, string DisplayName, string Description, string Module);
