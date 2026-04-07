namespace Appraisal.Application.Features.BlockCondo.GetCondoUnits;

/// <summary>
/// Queries DbContext directly (not through aggregate) for performance with 500+ units.
/// </summary>
public class GetCondoUnitsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetCondoUnitsQuery, GetCondoUnitsResult>
{
    public async Task<GetCondoUnitsResult> Handle(
        GetCondoUnitsQuery query,
        CancellationToken cancellationToken)
    {
        var units = await dbContext.CondoUnits
            .Where(u => u.AppraisalId == query.AppraisalId)
            .OrderBy(u => u.SequenceNumber)
            .Select(u => new CondoUnitDto(
                u.Id, u.AppraisalId, u.UploadBatchId, u.SequenceNumber,
                u.Floor, u.TowerName, u.CondoRegistrationNumber,
                u.RoomNumber, u.ModelType, u.UsableArea, u.SellingPrice))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var towers = units
            .Where(u => u.TowerName != null)
            .Select(u => u.TowerName!)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var models = units
            .Where(u => u.ModelType != null)
            .Select(u => u.ModelType!)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        return new GetCondoUnitsResult(units, towers, models, units.Count);
    }
}
