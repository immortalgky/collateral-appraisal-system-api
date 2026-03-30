using System.Threading;
using Shared.Pagination;

namespace Auth.Infrastructure.Repository;

public interface IPermissionRepository : IRepository<Permission, Guid>
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string permissionCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Permission>> GetPaginatedAsync(string? search, PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
}
