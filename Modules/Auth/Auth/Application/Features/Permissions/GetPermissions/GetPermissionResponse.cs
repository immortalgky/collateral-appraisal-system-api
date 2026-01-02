namespace Auth.Domain.Permissions.Features.GetPermissions;

public record GetPermissionResponse(PaginatedResult<PermissionDto> Result);
