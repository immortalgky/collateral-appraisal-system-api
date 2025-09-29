using System.Threading;

namespace OAuth2OpenId.Data.Repository;

public class PermissionRepository(OpenIddictDbContext dbContext)
    : BaseRepository<Permission, Guid>(dbContext),
        IPermissionRepository
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}
