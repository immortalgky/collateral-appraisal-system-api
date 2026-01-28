namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for MarketComparableFactor entity.
/// </summary>
public class MarketComparableFactorRepository(AppraisalDbContext dbContext)
    : BaseRepository<MarketComparableFactor, Guid>(dbContext), IMarketComparableFactorRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<MarketComparableFactor?> GetByCodeAsync(
        string factorCode,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparableFactors
            .FirstOrDefaultAsync(f => f.FactorCode == factorCode, cancellationToken);
    }

    public async Task<IEnumerable<MarketComparableFactor>> GetAllAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MarketComparableFactors.AsQueryable();

        if (activeOnly)
            query = query.Where(f => f.IsActive);

        return await query
            .OrderBy(f => f.FactorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MarketComparableFactor>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparableFactors
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(cancellationToken);
    }
}
