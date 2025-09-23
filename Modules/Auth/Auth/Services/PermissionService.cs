using OAuth2OpenId.Data.Repository;
using OAuth2OpenId.Identity.Models;

namespace Auth.Services;

public class PermissionService(
    IPermissionRepository permissionRepository,
    IPermissionReadRepository permissionReadRepository
) : IPermissionService
{
    public async Task<Permission> CreatePermission(
        PermissionDto permissionDto,
        CancellationToken cancellationToken = default
    )
    {
        var permission = new Permission
        {
            PermissionCode = permissionDto.PermissionCode,
            Description = permissionDto.Description,
        };
        await permissionRepository.AddAsync(permission, cancellationToken);
        await permissionRepository.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task<PaginatedResult<Permission>> GetPermissions(
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default
    )
    {
        var requests = await permissionReadRepository.GetPaginatedAsync(
            paginationRequest,
            cancellationToken
        );

        return requests;
    }

    public async Task<Permission?> GetPermissionById(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var permission = await permissionReadRepository.GetByIdAsync(id, cancellationToken);

        return permission;
    }

    public async Task DeletePermission(Guid id, CancellationToken cancellationToken = default)
    {
        await permissionRepository.DeleteAsync(id, cancellationToken);
        await permissionRepository.SaveChangesAsync(cancellationToken);
    }
}
