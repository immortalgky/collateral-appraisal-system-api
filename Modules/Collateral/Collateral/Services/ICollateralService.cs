namespace Collateral.Services;

public interface ICollateralService
{
    public Task<CollateralMaster> CreateCollateral(CollateralType collatType, CollateralDto collateral, CancellationToken cancellationToken = default);
}
