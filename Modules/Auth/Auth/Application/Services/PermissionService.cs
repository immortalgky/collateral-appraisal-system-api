using Microsoft.EntityFrameworkCore;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Identity;
using Auth.Infrastructure;
using Auth.Infrastructure.Repository;
using Shared.Exceptions;
using Shared.Pagination;

namespace Auth.Services;

public class PermissionService(
    IPermissionRepository permissionRepository,
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter) : IPermissionService
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
        auditWriter.Record(AuditAction.Created, AuditEntityType.Permission, permission.Id, permissionCode);
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
        auditWriter.Record(AuditAction.Updated, AuditEntityType.Permission, id, permission.PermissionCode);
        await permissionRepository.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task DeletePermission(Guid id, CancellationToken cancellationToken = default)
    {
        var permission = await permissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Permission", id);

        // Guard: refuse to delete a permission that is still attached to any role or user.
        var roleCount = await dbContext.RolePermissions.CountAsync(rp => rp.PermissionId == id, cancellationToken);
        var userCount = await dbContext.UserPermissions.CountAsync(up => up.PermissionId == id, cancellationToken);
        if (roleCount > 0 || userCount > 0)
        {
            var parts = new List<string>();
            if (roleCount > 0) parts.Add($"{roleCount} role(s)");
            if (userCount > 0) parts.Add($"{userCount} user(s)");
            throw new ConflictException(
                $"Cannot delete permission '{permission.PermissionCode}' because it is assigned to {string.Join(" and ", parts)}. Unassign it first.");
        }

        auditWriter.Record(AuditAction.Deleted, AuditEntityType.Permission, id, permission.PermissionCode);
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
