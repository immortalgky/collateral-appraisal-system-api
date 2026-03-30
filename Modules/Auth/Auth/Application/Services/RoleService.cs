using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Auth.Infrastructure.Repository;
using Auth.Domain.Identity;
using Shared.Exceptions;
using Shared.Pagination;

namespace Auth.Services;

public class RoleService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionRepository permissionRepository,
    AuthDbContext dbContext
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
    }

    public async Task UpdateRolePermissions(Guid id, List<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleById(id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        await PermissionService.ValidatePermissionsExistAsync(permissionIds, permissionRepository, cancellationToken);

        // Replace permission set
        dbContext.Set<RolePermission>().RemoveRange(role.Permissions);
        role.Permissions = [.. permissionIds.Select(pid => new RolePermission { RoleId = id, PermissionId = pid })];

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
    }

    public async Task DeleteRole(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleById(id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        await roleManager.DeleteAsync(role);
    }
}