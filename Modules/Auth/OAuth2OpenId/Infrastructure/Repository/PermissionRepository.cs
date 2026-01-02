using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Shared.Pagination;

namespace OAuth2OpenId.Data.Repository;

public class PermissionRepository(OpenIddictDbContext dbContext)
    : BaseRepository<Permission, Guid>(dbContext),
        IPermissionRepository
{
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions.AnyAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PaginatedResult<Permission>> GetPaginatedAsync(
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions
            .OrderBy(p => p.PermissionCode)
            .ToPaginatedResultAsync(paginationRequest, cancellationToken);
    }
}
