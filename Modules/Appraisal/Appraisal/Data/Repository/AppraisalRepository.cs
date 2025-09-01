using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Data.Repository;

public class AppraisalRepository : BaseRepository<RequestAppraisal, long>, IAppraisalRepository
{
    public AppraisalRepository(AppraisalDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<RequestAppraisal>> GetAllAsync(CancellationToken cancellationToken = default)
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

    protected override IQueryable<RequestAppraisal> GetReadQuery()
    {
        return base.GetReadQuery().Include(a => a.LandAppraisalDetail)
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
}