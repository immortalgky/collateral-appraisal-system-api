using Shared.Data;

namespace Collateral.Data.Repository;

public class CollateralRepository(CollateralDbContext context)
    : BaseRepository<CollateralMaster, long>(context),
        ICollateralRepository
{
    protected override IQueryable<CollateralMaster> GetReadQuery()
    {
        return base.GetReadQuery()
            .Include(c => c.CollateralLand)
            .Include(c => c.CollateralBuilding)
            .Include(c => c.CollateralCondo)
            .Include(c => c.CollateralMachine)
            .Include(c => c.CollateralVehicle)
            .Include(c => c.CollateralVessel)
            .Include(c => c.LandTitles);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}
