using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Shared.Pagination;

namespace Auth.Infrastructure.Repository;

public class PermissionRepository(AuthDbContext dbContext)
    : BaseRepository<Permission, Guid>(dbContext),
        IPermissionRepository
{
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions.AnyAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string permissionCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions.AnyAsync(
            p => p.PermissionCode == permissionCode && (excludeId == null || p.Id != excludeId),
            cancellationToken);
    }

    public async Task<PaginatedResult<Permission>> GetPaginatedAsync(
        string? search,
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Permissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.PermissionCode.Contains(search) || p.Description.Contains(search));

        return await query
            .OrderBy(p => p.PermissionCode)
            .ToPaginatedResultAsync(paginationRequest, cancellationToken);
    }
}
