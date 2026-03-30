namespace Auth.Application.Features.Permissions.GetPermissionById;

public record GetPermissionByIdResult(Guid Id, string PermissionCode, string DisplayName, string Description, string Module);
