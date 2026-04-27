namespace Appraisal.Application.Features.Project.GetProjectUnitPrices;

/// <summary>
/// Returns unit prices for all units in a project.
/// LEFT JOIN: every unit appears even before a ProjectUnitPrice row exists
/// (e.g. fresh upload before Calculate has been clicked).
/// Resolves ProjectId from AppraisalId via a preliminary Projects query.
/// </summary>
public class GetProjectUnitPricesQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetProjectUnitPricesQuery, GetProjectUnitPricesResult>
{
    public async Task<GetProjectUnitPricesResult> Handle(
        GetProjectUnitPricesQuery query,
        CancellationToken cancellationToken)
    {
        // Resolve ProjectId from AppraisalId (ProjectUnit has ProjectId FK, not AppraisalId)
        var projectId = await dbContext.Projects
            .Where(p => p.AppraisalId == query.AppraisalId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (projectId is null)
            return new GetProjectUnitPricesResult([]);

        var unitPrices = await (
            from unit in dbContext.ProjectUnits
            join price in dbContext.ProjectUnitPrices on unit.Id equals price.ProjectUnitId into pricesGroup
            from price in pricesGroup.DefaultIfEmpty()
            where unit.ProjectId == projectId.Value
            orderby unit.SequenceNumber
            select new ProjectUnitPriceDto(
                price != null ? (Guid?)price.Id : null,
                unit.Id,
                unit.SequenceNumber,
                // Common unit fields
                unit.ModelType,
                unit.UsableArea,
                unit.SellingPrice,
                // Condo-only unit fields
                unit.Floor,
                unit.TowerName,
                unit.CondoRegistrationNumber,
                unit.RoomNumber,
                // LB-only unit fields
                unit.PlotNumber,
                unit.HouseNumber,
                unit.NumberOfFloors,
                unit.LandArea,
                // Common flags
                price != null && price.IsCorner,
                price != null && price.IsEdge,
                price != null && price.IsOther,
                // Condo-only flags
                price != null && price.IsPoolView,
                price != null && price.IsSouth,
                // LB-only flags
                price != null && price.IsNearGarden,
                // Calculated values (common)
                price != null ? price.AdjustPriceLocation : null,
                price != null ? price.StandardPrice : null,
                price != null ? price.TotalAppraisalValue : null,
                price != null ? price.TotalAppraisalValueRounded : null,
                price != null ? price.ForceSellingPrice : null,
                price != null ? price.CoverageAmount : null,
                // Condo-only calculated
                price != null ? price.PriceIncrementPerFloor : null,
                // LB-only calculated
                price != null ? price.LandIncreaseDecreaseAmount : null)
        ).AsNoTracking().ToListAsync(cancellationToken);

        return new GetProjectUnitPricesResult(unitPrices);
    }
}
