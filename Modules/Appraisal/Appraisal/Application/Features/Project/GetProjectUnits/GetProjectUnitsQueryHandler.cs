namespace Appraisal.Application.Features.Project.GetProjectUnits;

/// <summary>
/// Queries DbContext directly (not through aggregate) for performance with large unit sets.
/// </summary>
public class GetProjectUnitsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetProjectUnitsQuery, GetProjectUnitsResult>
{
    public async Task<GetProjectUnitsResult> Handle(
        GetProjectUnitsQuery query,
        CancellationToken cancellationToken)
    {
        // Resolve ProjectId from AppraisalId
        var projectId = await dbContext.Projects
            .Where(p => p.AppraisalId == query.AppraisalId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (projectId is null)
            return new GetProjectUnitsResult([], [], [], 0);

        var units = await dbContext.ProjectUnits
            .Where(u => u.ProjectId == projectId.Value)
            .OrderBy(u => u.SequenceNumber)
            .Select(u => new ProjectUnitDto(
                u.Id, u.ProjectId, u.UploadBatchId, u.SequenceNumber,
                u.ModelType, u.UsableArea, u.SellingPrice,
                u.Floor, u.TowerName, u.CondoRegistrationNumber, u.RoomNumber,
                u.PlotNumber, u.HouseNumber, u.NumberOfFloors, u.LandArea))
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

        return new GetProjectUnitsResult(units, towers, models, units.Count);
    }
}
