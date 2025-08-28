using Shared.Data;

namespace Collateral.Data.Repository;

public interface ICollateralRepository : IRepository<CollateralMaster, long>
{
    Task<CollateralMaster> GetCollateralByIdAsync(
        long collatId,
        CancellationToken cancellationToken = default
    );
    Task<CollateralMaster> GetIncludedCollateralByIdAsync(
        long collatId,
        CancellationToken cancellationToken = default
    );
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
