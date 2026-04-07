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
            .ThenInclude(p => p.LandDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.BuildingDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.CondoDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.VehicleDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.VesselDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.MachineryDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.LeaseAgreementDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.UpFrontEntries)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.GrowthPeriodEntries)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.ScheduleEntries)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.ScheduleOverrides)
            .AsSplitQuery()
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
            .AsSplitQuery()
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
    public async Task<Domain.Appraisals.Appraisal?> GetByIdWithCondoDataAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.CondoProject)
            .Include(a => a.CondoModels)
            .ThenInclude(m => m.AreaDetails)
            .Include(a => a.CondoTowers)
            .Include(a => a.CondoUnits)
            .Include(a => a.CondoUnitUploads)
            .Include(a => a.CondoPricingAssumption)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByIdWithVillageDataAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.VillageProject)
            .Include(a => a.VillageProjectLand)
            .Include(a => a.VillageModels)
            .ThenInclude(m => m.AreaDetails)
            .Include(a => a.VillageModels)
            .ThenInclude(m => m.Surfaces)
            .Include(a => a.VillageModels)
            .ThenInclude(m => m.DepreciationDetails)
            .ThenInclude(d => d.DepreciationPeriods)
            .Include(a => a.VillageUnits)
            .Include(a => a.VillageUnitUploads)
            .Include(a => a.VillagePricingAssumption)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Appraisals.Appraisal>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .Include(a => a.Groups)
            .Include(a => a.Assignments)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }
}