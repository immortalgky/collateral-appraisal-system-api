namespace OAuth2OpenId.Data.Repository;

public interface IRoleRepository
{
    public Task<ApplicationRole?> GetRoleByName(string roleName);
}
