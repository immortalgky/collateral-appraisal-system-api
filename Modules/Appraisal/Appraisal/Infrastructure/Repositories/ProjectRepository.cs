namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository for the Project aggregate root.
/// Read-heavy patterns use AsSplitQuery to avoid Cartesian explosion on the full graph.
/// </summary>
public class ProjectRepository(AppraisalDbContext dbContext) : IProjectRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<Project?> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.AppraisalId == appraisalId, ct);
    }

    /// <inheritdoc />
    public async Task<Project?> GetWithFullGraphAsync(Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.Projects
            .Include(p => p.Towers)
                .ThenInclude(t => t.Images)
            .Include(p => p.Models)
                .ThenInclude(m => m.Images)
            .Include(p => p.Models)
                .ThenInclude(m => m.AreaDetails)
            .Include(p => p.Models)
                .ThenInclude(m => m.Surfaces)
            .Include(p => p.Models)
                .ThenInclude(m => m.DepreciationDetails)
                    .ThenInclude(d => d.DepreciationPeriods)
            .Include(p => p.Models)
                .ThenInclude(m => m.PricingAnalysis)
            .Include(p => p.Units)
            .Include(p => p.UnitUploads)
            // UnitPrices FK is ProjectUnitId → ProjectUnit (configured as 1:1).
            // Load via the DbSet directly when needed; not directly navigable from Project.
            .Include(p => p.PricingAssumption)
                .ThenInclude(a => a!.ModelAssumptions)
            .Include(p => p.Land)
                .ThenInclude(l => l!.Titles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.AppraisalId == appraisalId, ct);
    }

    /// <inheritdoc />
    public async Task<ProjectModel?> GetModelByIdWithImagesAsync(Guid modelId, CancellationToken ct = default)
    {
        return await _dbContext.ProjectModels
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == modelId, ct);
    }

    /// <inheritdoc />
    public async Task<ProjectTower?> GetTowerByIdWithImagesAsync(Guid towerId, CancellationToken ct = default)
    {
        return await _dbContext.ProjectTowers
            .Include(t => t.Images)
            .FirstOrDefaultAsync(t => t.Id == towerId, ct);
    }

    /// <inheritdoc />
    public void Add(Project project)
    {
        _dbContext.Projects.Add(project);
    }

    /// <inheritdoc />
    public void Update(Project project)
    {
        _dbContext.Projects.Update(project);
    }
}
