namespace Auth.Application.Features.Permissions.GetPermissionById;

public record GetPermissionByIdResponse(Guid Id, string PermissionCode, string DisplayName, string Description, string Module);
