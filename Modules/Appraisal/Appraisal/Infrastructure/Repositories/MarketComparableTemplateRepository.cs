namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for MarketComparableTemplate entity.
/// </summary>
public class MarketComparableTemplateRepository(AppraisalDbContext dbContext)
    : BaseRepository<MarketComparableTemplate, Guid>(dbContext), IMarketComparableTemplateRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<MarketComparableTemplate?> GetByCodeAsync(
        string templateCode,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparableTemplates
            .FirstOrDefaultAsync(t => t.TemplateCode == templateCode, cancellationToken);
    }

    public async Task<MarketComparableTemplate?> GetByIdWithFactorsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparableTemplates
            .Include(t => t.TemplateFactors)
                .ThenInclude(tf => tf.Factor)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<MarketComparableTemplate>> GetByPropertyTypeAsync(
        string propertyType,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MarketComparableTemplates
            .Include(t => t.TemplateFactors)
                .ThenInclude(tf => tf.Factor)
            .Where(t => t.PropertyType == propertyType);

        if (activeOnly)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MarketComparableTemplate>> GetAllAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MarketComparableTemplates
            .Include(t => t.TemplateFactors)
                .ThenInclude(tf => tf.Factor)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.PropertyType)
            .ThenBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);
    }
}
