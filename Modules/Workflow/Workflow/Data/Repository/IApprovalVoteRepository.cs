using Workflow.Domain;

namespace Workflow.Data.Repository;

public interface IApprovalVoteRepository
{
    Task<List<ApprovalVote>> GetVotesForExecutionAsync(Guid activityExecutionId, CancellationToken ct = default);
    Task<bool> HasMemberVotedAsync(Guid activityExecutionId, string member, CancellationToken ct = default);
    Task AddVoteAsync(ApprovalVote vote, CancellationToken ct = default);
}
