namespace Auth.Domain.Permissions.Features.CreatePermission;

public record CreatePermissionCommand(string PermissionCode, string Description)
    : ICommand<CreatePermissionResult>;
