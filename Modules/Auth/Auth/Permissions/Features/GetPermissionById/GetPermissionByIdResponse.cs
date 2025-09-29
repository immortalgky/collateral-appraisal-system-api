namespace Auth.Permissions.Features.GetPermissionById;

public record GetPermissionByIdResponse(Guid Id, string PermissionCode, string Description);
