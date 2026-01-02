using System.Linq.Expressions;
using Shared.Data;
using Shared.Pagination;

namespace Collateral.Data.Repository;

public class CollateralRepository(CollateralDbContext context)
    : BaseRepository<CollateralMaster, long>(context),
        ICollateralRepository
{
    protected IQueryable<CollateralMaster> GetReadQuery()
    {
        return IncludeQuery(Context.Set<CollateralMaster>().AsNoTracking());
    }

    protected IQueryable<CollateralMaster> GetTrackedQuery()
    {
        return IncludeQuery(Context.Set<CollateralMaster>());
    }

    private static IQueryable<CollateralMaster> IncludeQuery(IQueryable<CollateralMaster> query)
    {
        return query
            .Include(c => c.CollateralLand)
            .Include(c => c.CollateralBuilding)
            .Include(c => c.CollateralCondo)
            .Include(c => c.CollateralMachine)
            .Include(c => c.CollateralVehicle)
            .Include(c => c.CollateralVessel)
            .Include(c => c.LandTitles)
            .Include(c => c.CollateralEngagements);
    }

    public async Task<CollateralMaster?> GetByIdTrackedAsync(
        long id,
        CancellationToken cancellationToken = default
    )
    {
        return await GetTrackedQuery().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginatedResult<CollateralMaster>> GetPaginatedAsync(PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var query = GetReadQuery();
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<CollateralMaster>(items, totalCount, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<PaginatedResult<CollateralMaster>> GetPaginatedAsync(PaginationRequest pagination, Expression<Func<CollateralMaster, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var query = GetReadQuery().Where(predicate);
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<CollateralMaster>(items, totalCount, pagination.PageNumber, pagination.PageSize);
    }
}
