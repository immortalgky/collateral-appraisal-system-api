using System.Threading;
using Shared.Pagination;

namespace Auth.Infrastructure.Repository;

public interface IPermissionRepository : IRepository<Permission, Guid>
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Permission>> GetPaginatedAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
}
