namespace Auth.Domain.Permissions.Features.GetPermissions;

public record GetPermissionResult(PaginatedResult<PermissionDto> Result);
