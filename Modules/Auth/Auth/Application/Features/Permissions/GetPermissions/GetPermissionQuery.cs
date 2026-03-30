using Shared.Pagination;

namespace Auth.Application.Features.Permissions.GetPermissions;

public record GetPermissionQuery(string? Search, int PageNumber = 1, int PageSize = 20) : IQuery<GetPermissionResult>;
