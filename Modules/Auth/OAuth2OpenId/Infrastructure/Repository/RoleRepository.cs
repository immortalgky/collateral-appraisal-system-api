namespace OAuth2OpenId.Data.Repository;

public class RoleRepository(RoleManager<ApplicationRole> roleManager) : IRoleRepository
{
    public async Task<ApplicationRole?> GetRoleByName(string roleName)
    {
        return await roleManager
            .Roles.Include(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(role => role.Name == roleName);
    }
}
