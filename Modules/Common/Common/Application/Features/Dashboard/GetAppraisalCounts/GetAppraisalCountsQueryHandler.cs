using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.Dashboard.GetAppraisalCounts;

public class GetAppraisalCountsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider
) : IQueryHandler<GetAppraisalCountsQuery, GetAppraisalCountsResult>
{
    public async Task<GetAppraisalCountsResult> Handle(
        GetAppraisalCountsQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var from = query.From ?? dateTimeProvider.Today.AddYears(-1);
        var to = query.To ?? dateTimeProvider.Today;

        var (groupBy, selectPeriod) = query.Period?.ToLower() switch
        {
            "daily" => ("Date", "CONVERT(varchar, Date, 23)"),
            "yearly" => ("YEAR(Date)", "CAST(YEAR(Date) AS varchar)"),
            _ => ("YEAR(Date), MONTH(Date)", "CONCAT(YEAR(Date), '-', RIGHT('0' + CAST(MONTH(Date) AS varchar), 2))")
        };

        // Read from the view (drops the event-sourced read-model dependency).
        // In by-type mode we add AppraisalType to the projection and grouping so
        // the frontend can render one series per appraisal type.
        var selectType = query.GroupByType ? "AppraisalType," : "";
        var groupByType = query.GroupByType ? ", AppraisalType" : "";

        // Optional banking-segment filter. The view normalizes NULL → 'IBG', and we
        // compare case-insensitively so the frontend can pass canonical 'Retail'/'IBG'.
        var segmentFilter = string.IsNullOrWhiteSpace(query.BankingSegment)
            ? ""
            : " AND UPPER(BankingSegment) = UPPER(@BankingSegment)";

        var sql = $"""
            SELECT
                {selectPeriod} AS Period,
                {selectType}
                SUM(CreatedCount) AS CreatedCount,
                SUM(CompletedCount) AS CompletedCount
            FROM common.vw_DailyAppraisalCountsByType
            WHERE Date >= @From AND Date <= @To{segmentFilter}
            GROUP BY {groupBy}{groupByType}
            ORDER BY {groupBy}{groupByType}
            """;

        // Dapper doesn't support DateOnly — convert to DateTime
        var items = await connection.QueryAsync<AppraisalCountDto>(sql,
            new
            {
                From = from.ToDateTime(TimeOnly.MinValue),
                To = to.ToDateTime(TimeOnly.MinValue),
                query.BankingSegment
            });

        return new GetAppraisalCountsResult(items.ToList());
    }
}
