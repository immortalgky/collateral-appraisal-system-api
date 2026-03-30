using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Dashboard.GetAppraisalCounts;

public class GetAppraisalCountsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalCountsQuery, GetAppraisalCountsResult>
{
    public async Task<GetAppraisalCountsResult> Handle(
        GetAppraisalCountsQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var from = query.From ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var to = query.To ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var (groupBy, selectPeriod) = query.Period?.ToLower() switch
        {
            "daily" => ("Date", "CONVERT(varchar, Date, 23)"),
            "yearly" => ("YEAR(Date)", "CAST(YEAR(Date) AS varchar)"),
            _ => ("YEAR(Date), MONTH(Date)", "CONCAT(YEAR(Date), '-', RIGHT('0' + CAST(MONTH(Date) AS varchar), 2))")
        };

        var sql = $"""
            SELECT
                {selectPeriod} AS Period,
                SUM(CreatedCount) AS CreatedCount,
                SUM(CompletedCount) AS CompletedCount
            FROM common.DailyAppraisalCounts
            WHERE Date >= @From AND Date <= @To
            GROUP BY {groupBy}
            ORDER BY {groupBy}
            """;

        // Dapper doesn't support DateOnly — convert to DateTime
        var items = await connection.QueryAsync<AppraisalCountDto>(sql,
            new { From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) });

        return new GetAppraisalCountsResult(items.ToList());
    }
}
