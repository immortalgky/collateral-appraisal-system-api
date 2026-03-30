namespace Auth.Application.Features.Permissions.DeletePermission;

public record DeletePermissionCommand(Guid Id) : ICommand<DeletePermissionResult>;
