namespace Appraisal.Domain.Committees;

/// <summary>
/// Repository interface for Committee aggregate.
/// </summary>
public interface ICommitteeRepository : IRepository<Committee, Guid>
{
    Task<Committee?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Committee?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Committee>> GetActiveCommitteesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CommitteeVote>> GetVotesByReviewIdAsync(Guid reviewId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Committee>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CommitteeVote?> GetVoteByReviewAndMemberAsync(Guid reviewId, Guid committeeMemberId,
        CancellationToken cancellationToken = default);

    Task AddVoteAsync(CommitteeVote vote, CancellationToken cancellationToken = default);

    Task<Committee?> GetCommitteeForValueAsync(decimal value, CancellationToken cancellationToken = default);
}