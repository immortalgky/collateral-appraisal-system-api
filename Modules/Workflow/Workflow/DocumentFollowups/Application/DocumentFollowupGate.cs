using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application;

public class DocumentFollowupGate(WorkflowDbContext dbContext) : IDocumentFollowupGate
{
    public async Task<bool> HasOpenFollowupAsync(Guid raisingPendingTaskId, CancellationToken cancellationToken = default)
    {
        if (raisingPendingTaskId == Guid.Empty) return false;

        return await dbContext.DocumentFollowups
            .AsNoTracking()
            .AnyAsync(
                f => f.RaisingPendingTaskId == raisingPendingTaskId &&
                     f.Status == DocumentFollowupStatus.Open,
                cancellationToken);
    }
}
