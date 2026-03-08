namespace Appraisal.Infrastructure.Repositories;

public class AppraisalDecisionRepository(AppraisalDbContext dbContext)
    : BaseRepository<AppraisalDecision, Guid>(dbContext), IAppraisalDecisionRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<AppraisalDecision?> GetByAppraisalIdAsync(
        Guid appraisalId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppraisalDecisions
            .FirstOrDefaultAsync(d => d.AppraisalId == appraisalId, cancellationToken);
    }
}
