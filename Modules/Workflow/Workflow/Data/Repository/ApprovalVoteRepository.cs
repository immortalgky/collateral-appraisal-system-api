using Workflow.Domain;

namespace Workflow.Data.Repository;

public class ApprovalVoteRepository(WorkflowDbContext dbContext) : IApprovalVoteRepository
{
    public async Task<List<ApprovalVote>> GetVotesForExecutionAsync(Guid activityExecutionId,
        CancellationToken ct = default)
    {
        return await dbContext.ApprovalVotes
            .Where(v => v.ActivityExecutionId == activityExecutionId)
            .ToListAsync(ct);
    }

    public async Task<bool> HasMemberVotedAsync(Guid activityExecutionId, string member,
        CancellationToken ct = default)
    {
        return await dbContext.ApprovalVotes
            .AnyAsync(v => v.ActivityExecutionId == activityExecutionId
                && v.Member == member, ct);
    }

    public async Task AddVoteAsync(ApprovalVote vote, CancellationToken ct = default)
    {
        dbContext.ApprovalVotes.Add(vote);
        // Flush immediately so the unique index on (ActivityExecutionId, Member)
        // catches duplicates before the quorum/majority check proceeds.
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<ApprovalVote>> GetLatestRoundVotesByAppraisalAsync(
        Guid appraisalId, string activityId, CancellationToken ct = default)
    {
        var latestExecutionId = await dbContext.ApprovalVotes
            .Where(v => v.AppraisalId == appraisalId && v.ActivityId == activityId)
            .GroupBy(v => v.ActivityExecutionId)
            .OrderByDescending(g => g.Max(v => v.VotedAt))
            .Select(g => (Guid?)g.Key)
            .FirstOrDefaultAsync(ct);

        if (latestExecutionId is null) return new List<ApprovalVote>();

        return await dbContext.ApprovalVotes
            .Where(v => v.ActivityExecutionId == latestExecutionId.Value)
            .ToListAsync(ct);
    }
}
