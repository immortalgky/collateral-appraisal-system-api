using Shared.Data;

namespace Collateral.Data.Repository;

public interface ICollateralRepository : IRepository<CollateralMaster, long>
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
