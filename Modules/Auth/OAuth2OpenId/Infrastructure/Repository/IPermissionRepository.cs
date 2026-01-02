using System.Threading;
using Shared.Pagination;

namespace OAuth2OpenId.Data.Repository;

public interface IPermissionRepository : IRepository<Permission, Guid>
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Permission>> GetPaginatedAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
}
