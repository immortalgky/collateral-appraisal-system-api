using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OAuth2OpenId.Data;
using OAuth2OpenId.Data.Repository;
using OAuth2OpenId.Domain.Identity.Models;
using Shared.Exceptions;

namespace Auth.Domain.Auth.Features.Me;

public class MeQueryHandler(
    OpenIddictDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IRoleRepository roleRepository
) : IQueryHandler<MeQuery, MeResult>
{
    public async Task<MeResult> Handle(MeQuery query, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.Permissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken)
            ?? throw new NotFoundException("User", query.UserId);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await CalculatePermissions(user, roles);

        return new MeResult(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            roles.ToList(),
            permissions
        );
    }

    private async Task<List<string>> CalculatePermissions(ApplicationUser user, IList<string> roleNames)
    {
        var permissions = new HashSet<string>();
        var deniedPermissions = new HashSet<string>();

        // Collect user-level permissions
        foreach (var userPermission in user.Permissions)
        {
            if (userPermission.IsGranted)
                permissions.Add(userPermission.Permission.PermissionCode);
            else
                deniedPermissions.Add(userPermission.Permission.PermissionCode);
        }

        // Add role-level permissions (excluding explicitly denied ones)
        foreach (var roleName in roleNames)
        {
            var role = await roleRepository.GetRoleByName(roleName)
                ?? throw new NotFoundException("Role", roleName);

            var rolePermissionCodes = role.Permissions
                .Select(rp => rp.Permission.PermissionCode)
                .Where(code => !deniedPermissions.Contains(code));

            foreach (var code in rolePermissionCodes)
            {
                permissions.Add(code);
            }
        }

        return permissions.ToList();
    }
}
