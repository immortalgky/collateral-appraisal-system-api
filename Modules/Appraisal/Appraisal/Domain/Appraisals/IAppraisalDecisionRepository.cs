namespace Appraisal.Domain.Appraisals;

public interface IAppraisalDecisionRepository : IRepository<AppraisalDecision, Guid>
{
    Task<AppraisalDecision?> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
}
