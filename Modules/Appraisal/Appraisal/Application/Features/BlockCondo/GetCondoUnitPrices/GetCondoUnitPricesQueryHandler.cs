namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitPrices;

public class GetCondoUnitPricesQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetCondoUnitPricesQuery, GetCondoUnitPricesResult>
{
    public async Task<GetCondoUnitPricesResult> Handle(
        GetCondoUnitPricesQuery query,
        CancellationToken cancellationToken)
    {
        var unitPrices = await (
            from price in dbContext.CondoUnitPrices
            join unit in dbContext.CondoUnits on price.CondoUnitId equals unit.Id
            where unit.AppraisalId == query.AppraisalId
            orderby unit.SequenceNumber
            select new CondoUnitPriceDto(
                price.Id, price.CondoUnitId,
                unit.Floor, unit.TowerName, unit.RoomNumber,
                unit.ModelType, unit.UsableArea, unit.SellingPrice,
                price.IsCorner, price.IsEdge, price.IsPoolView,
                price.IsSouth, price.IsOther,
                price.AdjustPriceLocation, price.StandardPrice,
                price.PriceIncrementPerFloor,
                price.TotalAppraisalValue, price.TotalAppraisalValueRounded,
                price.ForceSellingPrice, price.CoverageAmount)
        ).AsNoTracking().ToListAsync(cancellationToken);

        return new GetCondoUnitPricesResult(unitPrices);
    }
}
