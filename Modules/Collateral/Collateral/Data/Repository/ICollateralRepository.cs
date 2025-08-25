namespace Collateral.Data.Repository;

public interface ICollateralRepository
{
    Task<CollateralMaster> CreateCollateralMasterAsync(CollateralMaster collateral, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}