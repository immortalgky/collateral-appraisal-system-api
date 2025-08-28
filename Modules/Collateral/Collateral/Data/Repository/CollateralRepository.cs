namespace Collateral.Data.Repository;

public class CollateralRepository(CollateralDbContext dbContext) : ICollateralRepository
{
    public async Task<CollateralMaster> CreateCollateralMasterAsync(CollateralMaster collateral, CancellationToken cancellationToken = default)
    {
        await dbContext.CollateralMasters.AddAsync(collateral, cancellationToken);
        return collateral;
    }

    public async Task<CollateralMaster?> GetNullableCollateralMasterByIdAsync(long collatId, CancellationToken cancellationToken = default) {
        return await dbContext.CollateralMasters.FindAsync([collatId], cancellationToken);
    }

    public async Task<CollateralMaster> GetCollateralMasterByIdAsync(long collatId, CancellationToken cancellationToken = default) {
        return await dbContext.CollateralMasters.FindAsync([collatId], cancellationToken) ?? throw new NotFoundException("Cannot find a collateral with this id.");
    }

    public async Task<bool> DeleteCollateralMasterAsync(long collatId, CancellationToken cancellationToken = default)
    {
        var collateral = await GetCollateralMasterByIdAsync(collatId, cancellationToken);
        dbContext.CollateralMasters.Remove(collateral);
        return true;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}