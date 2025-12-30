using OAuth2OpenId.Domain.Identity.Models;

namespace Auth.Services;

public interface IPermissionService
{
    public Task<Permission> CreatePermission(
        PermissionDto permissionDto,
        CancellationToken cancellationToken = default
    );
    public Task<PaginatedResult<Permission>> GetPermissions(
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default
    );
    public Task<Permission?> GetPermissionById(
        Guid id,
        CancellationToken cancellationToken = default
    );
    public Task DeletePermission(Guid id, CancellationToken cancellationToken = default);
}
