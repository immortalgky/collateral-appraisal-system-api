namespace Collateral.Data.Repository;

public interface ICollateralRepository
{
    Task<CollateralMaster> CreateCollateralMasterAsync(CollateralMaster collateral, CancellationToken cancellationToken = default);

    Task<CollateralMaster?> GetNullableCollateralMasterByIdAsync(long collatId, CancellationToken cancellationToken = default);
    Task<CollateralMaster> GetCollateralMasterByIdAsync(long collatId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCollateralMasterAsync(long collatId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}