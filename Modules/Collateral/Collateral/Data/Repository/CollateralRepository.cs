
namespace Collateral.Data.Repository;

public class CollateralRepository(CollateralDbContext dbContext) : ICollateralRepository
{
    public async Task<CollateralMaster> CreateCollateralMasterAsync(CollateralMaster collateral, CancellationToken cancellationToken = default)
    {
        dbContext.CollateralMasters.Add(collateral);
        await dbContext.SaveChangesAsync(cancellationToken);
        return collateral;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}