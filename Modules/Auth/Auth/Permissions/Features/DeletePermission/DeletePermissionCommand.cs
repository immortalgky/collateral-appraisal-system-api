namespace Auth.Permissions.Features.DeletePermission;

public record DeletePermissionCommand(Guid Id) : ICommand<DeletePermissionResult>;
