using System.Threading;

namespace OAuth2OpenId.Data.Repository;

public interface IPermissionReadRepository : IReadRepository<Permission, Guid>
{
    public Task<Permission?> GetByPermissionCodeAsync(
        string permissionCode,
        CancellationToken cancellationToken = default
    );
}
