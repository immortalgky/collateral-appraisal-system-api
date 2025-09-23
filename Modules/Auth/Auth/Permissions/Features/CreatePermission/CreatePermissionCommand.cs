namespace Auth.Permissions.Features.CreatePermission;

public record CreatePermissionCommand(string PermissionCode, string Description)
    : ICommand<CreatePermissionResult>;
