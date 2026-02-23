namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for PricingAnalysis aggregate
/// </summary>
public class PricingAnalysisRepository(AppraisalDbContext dbContext)
    : BaseRepository<Domain.Appraisals.PricingAnalysis, Guid>(dbContext), IPricingAnalysisRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Domain.Appraisals.PricingAnalysis?> GetByPropertyGroupIdAsync(Guid propertyGroupId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .FirstOrDefaultAsync(pa => pa.PropertyGroupId == propertyGroupId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.PricingAnalysis?> GetByIdWithAllDataAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.Calculations)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ComparableLinks)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ComparativeFactors)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.FactorScores)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.FinalValue)
            .AsSplitQuery()
            .FirstOrDefaultAsync(pa => pa.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByPropertyGroupIdAsync(Guid propertyGroupId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .AnyAsync(pa => pa.PropertyGroupId == propertyGroupId, cancellationToken);
    }
}
