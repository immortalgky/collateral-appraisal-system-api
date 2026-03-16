namespace Auth.Infrastructure.Repository;

public interface IRoleRepository
{
    public Task<ApplicationRole?> GetRoleByName(string roleName);
}
