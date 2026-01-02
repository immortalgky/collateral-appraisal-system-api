namespace Appraisal.Data.Repository;

public class AppraisalRepository : BaseRepository<RequestAppraisal, long>, IAppraisalRepository
{
    public AppraisalRepository(AppraisalDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<RequestAppraisal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<RequestAppraisal>()
            .AsNoTracking()
            .Include(a => a.LandAppraisalDetail)
            .Include(a => a.BuildingAppraisalDetail)
            .Include(a => a.CondoAppraisalDetail)
            .Include(a => a.MachineAppraisalDetail)
            .Include(a => a.MachineAppraisalAdditionalInfo)
            .Include(a => a.VehicleAppraisalDetail)
            .Include(a => a.VesselAppraisalDetail)
            .ToListAsync(cancellationToken);
    }

    protected IQueryable<RequestAppraisal> GetReadQuery()
    {
        return Context.Set<RequestAppraisal>()
            .Include(a => a.LandAppraisalDetail)
            .Include(a => a.BuildingAppraisalDetail)
            .Include(a => a.CondoAppraisalDetail)
            .Include(a => a.MachineAppraisalDetail)
            .Include(a => a.MachineAppraisalAdditionalInfo)
            .Include(a => a.VehicleAppraisalDetail)
            .Include(a => a.VesselAppraisalDetail);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RequestAppraisal>> GetByCollateralIdAsync(long collatId, CancellationToken cancellationToken = default)
    {
        return await GetReadQuery()
            .AsNoTracking()
            .Where(a => a.CollateralId == collatId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedResult<RequestAppraisal>> GetPaginatedAsync(PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var query = GetReadQuery().AsNoTracking();
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<RequestAppraisal>(items, totalCount, pagination.PageNumber, pagination.PageSize);
    }
}