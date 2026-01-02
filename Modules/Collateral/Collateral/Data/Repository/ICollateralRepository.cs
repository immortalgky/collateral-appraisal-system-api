using System.Linq.Expressions;
using Shared.Data;
using Shared.Pagination;

namespace Collateral.Data.Repository;

public interface ICollateralRepository : IRepository<CollateralMaster, long>
{
    Task<CollateralMaster?> GetByIdTrackedAsync(
        long id,
        CancellationToken cancellationToken = default
    );
    new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<PaginatedResult<CollateralMaster>> GetPaginatedAsync(PaginationRequest pagination, CancellationToken cancellationToken = default);

    Task<PaginatedResult<CollateralMaster>> GetPaginatedAsync(PaginationRequest pagination, Expression<Func<CollateralMaster, bool>> predicate, CancellationToken cancellationToken = default);
}
