using System.Threading;

namespace OAuth2OpenId.Data.Repository;

public class PermissionReadRepository(OpenIddictDbContext dbContext)
    : BaseReadRepository<Permission, Guid>(dbContext),
        IPermissionReadRepository
{
    public async Task<Permission?> GetByPermissionCodeAsync(
        string permissionCode,
        CancellationToken cancellationToken = default
    )
    {
        return await dbContext.Permissions.FirstOrDefaultAsync(
            permission => permission.PermissionCode == permissionCode,
            cancellationToken
        );
    }
}
