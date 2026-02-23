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
}