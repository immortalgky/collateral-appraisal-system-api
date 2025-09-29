using Shared.Data;

namespace Collateral.Data.Repository;

public class CollateralRepository(CollateralDbContext context)
    : BaseRepository<CollateralMaster, long>(context),
        ICollateralRepository
{
    protected override IQueryable<CollateralMaster> GetReadQuery()
    {
        return IncludeQuery(base.GetReadQuery());
    }

    protected override IQueryable<CollateralMaster> GetTrackedQuery()
    {
        return IncludeQuery(base.GetTrackedQuery());
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
}
