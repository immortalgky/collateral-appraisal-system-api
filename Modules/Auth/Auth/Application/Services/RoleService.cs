using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Auth.Application.Services;
using Auth.Infrastructure.Repository;
using Auth.Domain.Identity;
using Auth.Domain.Auditing;
using Shared.Exceptions;
using Shared.Pagination;

namespace Auth.Services;

public class RoleService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionRepository permissionRepository,
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter
) : IRoleService
{
    public async Task<ApplicationRole> CreateRole(
        string name,
        string description,
        string? scope,
        List<Guid> permissionIds,
        CancellationToken cancellationToken = default)
    {
        await PermissionService.ValidatePermissionsExistAsync(permissionIds, permissionRepository, cancellationToken);

        var role = new ApplicationRole
        {
            Name = name,
            Description = description,
            Scope = scope,
            Permissions = [.. permissionIds.Select(id => new RolePermission { PermissionId = id })]
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));

        auditWriter.Record(AuditAction.Created, AuditEntityType.Role, role.Id, name);
        // roleManager.CreateAsync flushes via Identity's store — audit row written in next SaveChanges.
        // RoleService has no separate SaveChangesAsync call; enqueue only (Identity commits independently).
        // To flush the audit row we call dbContext.SaveChangesAsync here.
        await dbContext.SaveChangesAsync(cancellationToken);

        return role;
    }

    public async Task<PaginatedResult<ApplicationRole>> GetRoles(
        string? search,
        string? scope,
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default)
    {
        var query = roleManager.Roles
            .Include(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Name!.Contains(search) || r.Description.Contains(search));

        if (!string.IsNullOrWhiteSpace(scope))
            query = query.Where(r => r.Scope == scope || r.Scope == null);

        return await query.OrderBy(r => r.Name).ToPaginatedResultAsync(paginationRequest, cancellationToken);
    }

    public async Task<ApplicationRole?> GetRoleById(Guid id, CancellationToken cancellationToken = default)
    {
        return await roleManager.Roles
            .Include(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task UpdateRole(Guid id, string name, string description, string? scope, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("Role", id);

        role.Name = name;
        role.Description = description;
        role.Scope = scope;

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        auditWriter.Record(AuditAction.Updated, AuditEntityType.Role, id, name);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRolePermissions(Guid id, List<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleById(id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        await PermissionService.ValidatePermissionsExistAsync(permissionIds, permissionRepository, cancellationToken);

        var beforeIds = role.Permissions.Select(rp => rp.PermissionId).ToList();

        // Replace permission set
        dbContext.Set<RolePermission>().RemoveRange(role.Permissions);
        role.Permissions = [.. permissionIds.Select(pid => new RolePermission { RoleId = id, PermissionId = pid })];

        auditWriter.RecordAssignmentChange(AuditEntityType.Role, id, role.Name, beforeIds, permissionIds, "permissions");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ApplicationUser>> GetRoleUsers(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("Role", id);

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        return [.. usersInRole];
    }

    public async Task UpdateRoleUsers(Guid id, List<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("Role", id);

        var roleName = role.Name!;
        var currentUsers = await userManager.GetUsersInRoleAsync(roleName);

        var beforeIds = currentUsers.Select(u => u.Id).ToList();

        // Remove users no longer in list
        foreach (var user in currentUsers)
        {
            if (!userIds.Contains(user.Id))
            {
                var result = await userManager.RemoveFromRoleAsync(user, roleName);
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        // Add new users
        var currentUserIds = currentUsers.Select(u => u.Id).ToHashSet();
        foreach (var userId in userIds)
        {
            if (currentUserIds.Contains(userId)) continue;

            var user = await userManager.FindByIdAsync(userId.ToString())
                ?? throw new NotFoundException("User", userId);

            var result = await userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        auditWriter.RecordAssignmentChange(AuditEntityType.Role, id, roleName, beforeIds, userIds, "users");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRole(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleById(id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        // Guard: refuse to delete a role that is still assigned to users.
        var userCount = await dbContext.UserRoles.CountAsync(ur => ur.RoleId == id, cancellationToken);
        if (userCount > 0)
            throw new ConflictException(
                $"Cannot delete role '{role.Name}' because it is assigned to {userCount} user(s). Unassign them first.");

        // Delete first — only audit on confirmed success
        var deleteResult = await roleManager.DeleteAsync(role);
        if (!deleteResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", deleteResult.Errors.Select(e => e.Description)));

        auditWriter.Record(AuditAction.Deleted, AuditEntityType.Role, id, role.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
