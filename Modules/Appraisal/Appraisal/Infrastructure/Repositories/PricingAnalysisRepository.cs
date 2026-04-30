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
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.RsqResult)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.MachineCostItems)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.LeaseholdAnalysis!)
                        .ThenInclude(l => l.LandGrowthPeriods)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.LeaseholdAnalysis!)
                        .ThenInclude(l => l.TableRows)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ProfitRentAnalysis!)
                        .ThenInclude(p => p.GrowthPeriods)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ProfitRentAnalysis!)
                        .ThenInclude(p => p.TableRows)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.IncomeAnalysis!)
                        .ThenInclude(ia => ia.Sections)
                            .ThenInclude(s => s.Categories)
                                .ThenInclude(c => c.Assumptions)
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

    /// <inheritdoc />
    public async Task<PricingAnalysis?> GetByProjectModelIdAsync(Guid projectModelId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .FirstOrDefaultAsync(pa => pa.ProjectModelId == projectModelId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByProjectModelIdAsync(Guid projectModelId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .AnyAsync(pa => pa.ProjectModelId == projectModelId, cancellationToken);
    }
}
