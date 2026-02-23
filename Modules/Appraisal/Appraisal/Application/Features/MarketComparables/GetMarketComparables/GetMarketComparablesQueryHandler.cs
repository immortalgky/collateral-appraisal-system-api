namespace Appraisal.Application.Features.MarketComparables.GetMarketComparables;

/// <summary>
/// Handler for getting all Market Comparables with pagination.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetMarketComparablesQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetMarketComparablesQuery, GetMarketComparablesResult>
{
    public async Task<GetMarketComparablesResult> Handle(
        GetMarketComparablesQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM appraisal.vw_MarketComparableList";

        var result = await connectionFactory.QueryPaginatedAsync<MarketComparableDto>(
            sql,
            "CreatedAt DESC",
            query.PaginationRequest);

        return new GetMarketComparablesResult(result);
    }
}