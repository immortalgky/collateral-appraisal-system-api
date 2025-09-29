using Shared.Data;

namespace Collateral.Data.Repository;

public interface ICollateralRepository : IRepository<CollateralMaster, long>
{
    Task<CollateralMaster?> GetByIdTrackedAsync(
        long id,
        CancellationToken cancellationToken = default
    );
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
