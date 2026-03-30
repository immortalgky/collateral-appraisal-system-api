using Auth.Domain.Identity;
using Shared.Pagination;

namespace Auth.Services;

public interface IRoleService
{
    Task<ApplicationRole> CreateRole(string name, string description, string? scope, List<Guid> permissionIds, CancellationToken cancellationToken = default);
    Task<PaginatedResult<ApplicationRole>> GetRoles(string? search, string? scope, PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
    Task<ApplicationRole?> GetRoleById(Guid id, CancellationToken cancellationToken = default);
    Task UpdateRole(Guid id, string name, string description, string? scope, CancellationToken cancellationToken = default);
    Task UpdateRolePermissions(Guid id, List<Guid> permissionIds, CancellationToken cancellationToken = default);
    Task<List<ApplicationUser>> GetRoleUsers(Guid id, CancellationToken cancellationToken = default);
    Task UpdateRoleUsers(Guid id, List<Guid> userIds, CancellationToken cancellationToken = default);
    Task DeleteRole(Guid id, CancellationToken cancellationToken = default);
}