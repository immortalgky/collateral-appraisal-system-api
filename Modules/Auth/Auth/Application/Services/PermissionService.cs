using OAuth2OpenId.Data.Repository;
using OAuth2OpenId.Domain.Identity.Models;
using Shared.Exceptions;

namespace Auth.Services;

public class PermissionService(
    IPermissionRepository permissionRepository
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
        var requests = await permissionRepository.GetPaginatedAsync(
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
        var permission = await permissionRepository.GetByIdAsync(id, cancellationToken);

        return permission;
    }

    public async Task DeletePermission(Guid id, CancellationToken cancellationToken = default)
    {
        await permissionRepository.DeleteAsync(id, cancellationToken);
        await permissionRepository.SaveChangesAsync(cancellationToken);
    }

    internal static async Task ValidatePermissionsExistAsync(
        List<Guid> permissionsIds,
        IPermissionRepository permissionRepository,
        CancellationToken cancellationToken
    )
    {
        foreach (var permissionId in permissionsIds)
        {
            var isPermissionExisted = await permissionRepository.ExistsAsync(
                permissionId,
                cancellationToken
            );
            if (!isPermissionExisted)
            {
                throw new NotFoundException("Permission", permissionId);
            }
        }
    }
}
