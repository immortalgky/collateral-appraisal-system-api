using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.GetVillageUnits;

/// <summary>
/// Queries DbContext directly for performance with large unit sets.
/// </summary>
public class GetVillageUnitsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetVillageUnitsQuery, GetVillageUnitsResult>
{
    public async Task<GetVillageUnitsResult> Handle(
        GetVillageUnitsQuery query,
        CancellationToken cancellationToken)
    {
        var units = await dbContext.VillageUnits
            .Where(u => u.AppraisalId == query.AppraisalId)
            .OrderBy(u => u.SequenceNumber)
            .Select(u => new VillageUnitDto(
                u.Id, u.AppraisalId, u.UploadBatchId, u.SequenceNumber,
                u.PlotNumber, u.HouseNumber, u.ModelName,
                u.NumberOfFloors, u.LandArea, u.UsableArea, u.SellingPrice))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var modelNames = units
            .Where(u => u.ModelName != null)
            .Select(u => u.ModelName!)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        return new GetVillageUnitsResult(units, modelNames, units.Count);
    }
}
