namespace Auth.Application.Features.Permissions.CreatePermission;

public record CreatePermissionCommand(string PermissionCode, string DisplayName, string Description, string Module)
    : ICommand<CreatePermissionResult>;
