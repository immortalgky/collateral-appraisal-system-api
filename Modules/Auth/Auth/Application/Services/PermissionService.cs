using Auth.Domain.Identity;
using Auth.Infrastructure.Repository;
using Shared.Exceptions;
using Shared.Pagination;

namespace Auth.Services;

public class PermissionService(IPermissionRepository permissionRepository) : IPermissionService
{
    public async Task<Permission> CreatePermission(
        string permissionCode,
        string displayName,
        string description,
        string module,
        CancellationToken cancellationToken = default)
    {
        var permission = Permission.Create(permissionCode, displayName, description, module);
        await permissionRepository.AddAsync(permission, cancellationToken);
        await permissionRepository.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task<PaginatedResult<Permission>> GetPermissions(
        string? search,
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default)
    {
        return await permissionRepository.GetPaginatedAsync(search, paginationRequest, cancellationToken);
    }

    public async Task<Permission?> GetPermissionById(Guid id, CancellationToken cancellationToken = default)
    {
        return await permissionRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Permission> UpdatePermission(
        Guid id,
        string displayName,
        string description,
        string module,
        CancellationToken cancellationToken = default)
    {
        var permission = await permissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Permission", id);

        permission.Update(displayName, description, module);
        await permissionRepository.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task DeletePermission(Guid id, CancellationToken cancellationToken = default)
    {
        var permission = await permissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Permission", id);

        await permissionRepository.DeleteAsync(permission, cancellationToken);
        await permissionRepository.SaveChangesAsync(cancellationToken);
    }

    internal static async Task ValidatePermissionsExistAsync(
        List<Guid> permissionsIds,
        IPermissionRepository permissionRepository,
        CancellationToken cancellationToken)
    {
        foreach (var permissionId in permissionsIds)
        {
            if (!await permissionRepository.ExistsAsync(permissionId, cancellationToken))
                throw new NotFoundException("Permission", permissionId);
        }
    }
}