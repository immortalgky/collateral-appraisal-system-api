namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Committee aggregate.
/// </summary>
public class CommitteeRepository(AppraisalDbContext dbContext)
    : BaseRepository<Committee, Guid>(dbContext), ICommitteeRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Committee?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Committees
            .FirstOrDefaultAsync(c => c.CommitteeCode == code, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Committee?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Committees
            .Include(c => c.Members)
            .Include(c => c.Conditions)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Committee>> GetActiveCommitteesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Committees
            .Where(c => c.IsActive)
            .Include(c => c.Members.Where(m => m.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CommitteeVote>> GetVotesByReviewIdAsync(Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CommitteeVotes
            .Where(v => v.ReviewId == reviewId)
            .OrderBy(v => v.VotedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Committee>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Committees
            .Include(c => c.Members)
            .Include(c => c.Conditions)
            .OrderBy(c => c.CommitteeName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CommitteeVote?> GetVoteByReviewAndMemberAsync(Guid reviewId, Guid committeeMemberId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CommitteeVotes
            .FirstOrDefaultAsync(v => v.ReviewId == reviewId && v.CommitteeMemberId == committeeMemberId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddVoteAsync(CommitteeVote vote, CancellationToken cancellationToken = default)
    {
        await _dbContext.CommitteeVotes.AddAsync(vote, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Committee?> GetCommitteeForValueAsync(decimal value,
        CancellationToken cancellationToken = default)
    {
        var threshold = await _dbContext.CommitteeThresholds
            .Where(t => t.IsActive && t.MinValue <= value && (t.MaxValue == null || t.MaxValue > value))
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync(cancellationToken);

        if (threshold == null) return null;

        return await GetByIdWithMembersAsync(threshold.CommitteeId, cancellationToken);
    }
}