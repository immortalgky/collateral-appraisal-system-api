using Shared.Data;

namespace Collateral.Data.Repository;

public class CollateralRepository(CollateralDbContext context)
    : BaseRepository<CollateralMaster, long>(context),
        ICollateralRepository
{
    public async Task<CollateralMaster> GetCollateralByIdAsync(
        long collatId,
        CancellationToken cancellationToken = default
    )
    {
        return await context.CollateralMasters.FindAsync([collatId], cancellationToken)
            ?? throw new NotFoundException("Cannot find a collateral with this id.");
    }

    public async Task<CollateralMaster> GetIncludedCollateralByIdAsync(
        long collatId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
                .CollateralMasters.Include(c => c.CollateralLand)
                .Include(c => c.CollateralBuilding)
                .Include(c => c.CollateralCondo)
                .Include(c => c.CollateralMachine)
                .Include(c => c.CollateralVehicle)
                .Include(c => c.CollateralVessel)
                .Include(c => c.LandTitles)
                .Where(c => c.Id == collatId)
                .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cannot find a collateral with this id.");
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}
