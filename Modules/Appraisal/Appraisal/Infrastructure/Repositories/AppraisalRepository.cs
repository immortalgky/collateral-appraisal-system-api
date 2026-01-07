namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Appraisal aggregate
/// </summary>
public class AppraisalRepository(AppraisalDbContext dbContext)
    : BaseRepository<Domain.Appraisals.Appraisal, Guid>(dbContext), IAppraisalRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByIdWithPropertiesAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByIdWithAllDataAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .Include(a => a.Groups)
            .ThenInclude(g => g.Items)
            .Include(a => a.Assignments)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByAppraisalNumberAsync(string appraisalNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .FirstOrDefaultAsync(a => a.AppraisalNumber == appraisalNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Appraisals.Appraisal>> GetByRequestIdAsync(Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Where(a => a.RequestId == requestId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .AnyAsync(a => a.RequestId == requestId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Appraisals.Appraisal>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .Include(a => a.Groups)
            .Include(a => a.Assignments)
            .ToListAsync(cancellationToken);
    }
}