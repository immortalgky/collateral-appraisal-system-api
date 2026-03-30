using Auth.Domain.Identity;
using Shared.Pagination;

namespace Auth.Services;

public interface IPermissionService
{
    Task<Permission> CreatePermission(string permissionCode, string displayName, string description, string module, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Permission>> GetPermissions(string? search, PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
    Task<Permission?> GetPermissionById(Guid id, CancellationToken cancellationToken = default);
    Task<Permission> UpdatePermission(Guid id, string displayName, string description, string module, CancellationToken cancellationToken = default);
    Task DeletePermission(Guid id, CancellationToken cancellationToken = default);
}