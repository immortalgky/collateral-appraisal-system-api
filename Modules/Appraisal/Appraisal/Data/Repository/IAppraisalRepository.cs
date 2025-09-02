namespace Appraisal.Data.Repository;

public interface IAppraisalRepository : IRepository<RequestAppraisal, long>
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<List<RequestAppraisal>> GetByCollateralIdAsync(long collatId, CancellationToken cancellationToken = default);
}