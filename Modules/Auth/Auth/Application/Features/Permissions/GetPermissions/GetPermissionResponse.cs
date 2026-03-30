namespace Auth.Application.Features.Permissions.GetPermissions;

public record GetPermissionResponse(IEnumerable<PermissionItemDto> Items, long Count, int PageNumber, int PageSize);
