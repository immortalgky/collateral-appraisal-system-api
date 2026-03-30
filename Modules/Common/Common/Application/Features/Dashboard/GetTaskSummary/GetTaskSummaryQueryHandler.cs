using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.GetTaskSummary;

public class GetTaskSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetTaskSummaryQuery, GetTaskSummaryResult>
{
    public async Task<GetTaskSummaryResult> Handle(
        GetTaskSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetTaskSummaryResult([]);

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
                SUM(NotStarted) AS NotStarted,
                SUM(InProgress) AS InProgress,
                SUM(Overdue) AS Overdue,
                SUM(Completed) AS Completed
            FROM common.DailyTaskSummaries
            WHERE Username = @Username AND Date >= @From AND Date <= @To
            GROUP BY {groupBy}
            ORDER BY {groupBy}
            """;

        // Dapper doesn't support DateOnly — convert to DateTime
        var items = await connection.QueryAsync<TaskSummaryDto>(sql,
            new { Username = username, From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) });

        return new GetTaskSummaryResult(items.ToList());
    }
}
