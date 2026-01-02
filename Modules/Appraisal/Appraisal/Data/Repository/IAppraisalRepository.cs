using Shared.Pagination;

namespace Appraisal.Data.Repository;

public interface IAppraisalRepository : IRepository<RequestAppraisal, long>
{
    new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<List<RequestAppraisal>> GetByCollateralIdAsync(long collatId, CancellationToken cancellationToken = default);

    Task<PaginatedResult<RequestAppraisal>> GetPaginatedAsync(PaginationRequest pagination, CancellationToken cancellationToken = default);
}