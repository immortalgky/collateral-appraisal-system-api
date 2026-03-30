using Shared.Pagination;

namespace Auth.Application.Features.Permissions.GetPermissions;

public record GetPermissionResult(IEnumerable<PermissionItemDto> Items, long Count, int PageNumber, int PageSize);

public record PermissionItemDto(Guid Id, string PermissionCode, string DisplayName, string Description, string Module);
