using System.Threading;

namespace OAuth2OpenId.Data.Repository;

public interface IPermissionRepository : IRepository<Permission, Guid>
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
