namespace Auth.Application.Features.Permissions.UpdatePermission;

public record UpdatePermissionCommand(Guid Id, string DisplayName, string Description, string Module)
    : ICommand<UpdatePermissionResult>;
