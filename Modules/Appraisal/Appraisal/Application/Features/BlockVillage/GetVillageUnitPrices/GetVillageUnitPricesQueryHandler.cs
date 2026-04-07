namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitPrices;

public class GetVillageUnitPricesQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetVillageUnitPricesQuery, GetVillageUnitPricesResult>
{
    public async Task<GetVillageUnitPricesResult> Handle(
        GetVillageUnitPricesQuery query,
        CancellationToken cancellationToken)
    {
        var unitPrices = await (
            from price in dbContext.VillageUnitPrices
            join unit in dbContext.VillageUnits on price.VillageUnitId equals unit.Id
            where unit.AppraisalId == query.AppraisalId
            orderby unit.SequenceNumber
            select new VillageUnitPriceDto(
                price.Id, price.VillageUnitId,
                unit.SequenceNumber, unit.PlotNumber, unit.HouseNumber,
                unit.ModelName, unit.NumberOfFloors, unit.LandArea,
                unit.UsableArea, unit.SellingPrice,
                price.IsCorner, price.IsEdge, price.IsNearGarden, price.IsOther,
                price.LandIncreaseDecreaseAmount, price.AdjustPriceLocation,
                price.StandardPrice,
                price.TotalAppraisalValue, price.TotalAppraisalValueRounded,
                price.ForceSellingPrice, price.CoverageAmount)
        ).AsNoTracking().ToListAsync(cancellationToken);

        return new GetVillageUnitPricesResult(unitPrices);
    }
}
