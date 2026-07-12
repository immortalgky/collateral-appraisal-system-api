namespace Parameter.Dealers.Features.GetDealers;

public class GetDealersQueryHandler(
    ParameterDbContext context
) : IQueryHandler<GetDealersQuery, GetDealersResult>
{
    public async Task<GetDealersResult> Handle(
        GetDealersQuery query,
        CancellationToken cancellationToken)
    {
        var dealers = await context.Dealers
            .OrderBy(d => d.DealerCode)
            .Select(d => new DealerDto(d.DealerCode, d.DealerName))
            .ToListAsync(cancellationToken);

        return new GetDealersResult(dealers);
    }
}
