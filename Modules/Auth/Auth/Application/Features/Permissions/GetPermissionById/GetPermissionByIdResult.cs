namespace Auth.Domain.Permissions.Features.GetPermissionById;

public record GetPermissionByIdResult(Guid Id, string PermissionCode, string Description);
