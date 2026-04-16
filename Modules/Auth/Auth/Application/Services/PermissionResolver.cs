using Auth.Domain.Identity;
using Auth.Infrastructure.Repository;
using Shared.Exceptions;

namespace Auth.Application.Services;

/// <summary>
/// Resolves the effective set of permission codes for a user, combining
/// user-level grants/denies with role-level permissions. Extracted from
/// MeQueryHandler so both /auth/me and /auth/me/menu share one implementation.
/// </summary>
public class PermissionResolver(IRoleRepository roleRepository)
{
    public async Task<HashSet<string>> CalculateAsync(ApplicationUser user, IEnumerable<string> roleNames)
    {
        var permissions = new HashSet<string>();
        var deniedPermissions = new HashSet<string>();

        // Collect user-level permissions
        foreach (var userPermission in user.Permissions)
            if (userPermission.IsGranted)
                permissions.Add(userPermission.Permission.PermissionCode);
            else
                deniedPermissions.Add(userPermission.Permission.PermissionCode);

        // Add role-level permissions (excluding explicitly denied ones)
        foreach (var roleName in roleNames)
        {
            var role = await roleRepository.GetRoleByName(roleName)
                       ?? throw new NotFoundException("Role", roleName);

            var rolePermissionCodes = role.Permissions
                .Select(rp => rp.Permission.PermissionCode)
                .Where(code => !deniedPermissions.Contains(code));

            foreach (var code in rolePermissionCodes) permissions.Add(code);
        }

        return permissions;
    }
}
