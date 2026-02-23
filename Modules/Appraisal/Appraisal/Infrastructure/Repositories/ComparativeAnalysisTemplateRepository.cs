using Appraisal.Domain.ComparativeAnalysis;

namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ComparativeAnalysisTemplate aggregate
/// </summary>
public class ComparativeAnalysisTemplateRepository(AppraisalDbContext dbContext)
    : IComparativeAnalysisTemplateRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<ComparativeAnalysisTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ComparativeAnalysisTemplate?> GetByIdWithFactorsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .Include(t => t.Factors)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ComparativeAnalysisTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .Include(t => t.Factors)
            .FirstOrDefaultAsync(t => t.TemplateCode == templateCode.ToUpperInvariant(), cancellationToken);
    }

    public async Task<ComparativeAnalysisTemplate?> GetByPropertyTypeAsync(string propertyType, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .Include(t => t.Factors)
            .FirstOrDefaultAsync(t => t.PropertyType == propertyType && t.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<ComparativeAnalysisTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .Include(t => t.Factors)
            .OrderBy(t => t.PropertyType)
            .ThenBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComparativeAnalysisTemplate>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .Include(t => t.Factors)
            .Where(t => t.IsActive)
            .OrderBy(t => t.PropertyType)
            .ThenBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByTemplateCodeAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComparativeAnalysisTemplates
            .AnyAsync(t => t.TemplateCode == templateCode.ToUpperInvariant(), cancellationToken);
    }

    public void Add(ComparativeAnalysisTemplate template)
    {
        _dbContext.ComparativeAnalysisTemplates.Add(template);
    }

    public void Update(ComparativeAnalysisTemplate template)
    {
        _dbContext.ComparativeAnalysisTemplates.Update(template);
    }

    public void Delete(ComparativeAnalysisTemplate template)
    {
        _dbContext.ComparativeAnalysisTemplates.Remove(template);
    }
}
