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
    // Static SQL fragments per granularity — no user input reaches the SQL string.
    // Runtime values (Username, From, To) are passed via DynamicParameters.
    private static readonly Dictionary<string, (string GroupBy, string SelectPeriod)> PeriodFragments = new()
    {
        ["daily"]   = ("CAST(EventAt AS date)",
                       "CONVERT(varchar, CAST(EventAt AS date), 23)"),
        ["weekly"]  = ("DATEPART(iso_week, EventAt), YEAR(EventAt)",
                       "CONCAT(YEAR(EventAt), '-W', RIGHT('0' + CAST(DATEPART(iso_week, EventAt) AS varchar), 2))"),
        ["monthly"] = ("YEAR(EventAt), MONTH(EventAt)",
                       "CONCAT(YEAR(EventAt), '-', RIGHT('0' + CAST(MONTH(EventAt) AS varchar), 2))"),
        ["yearly"]  = ("YEAR(EventAt)",
                       "CAST(YEAR(EventAt) AS varchar)")
    };

    public async Task<GetTaskSummaryResult> Handle(
        GetTaskSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetTaskSummaryResult([]);

        var connection = connectionFactory.GetOpenConnection();
        var period = query.Period?.ToLower() ?? "monthly";

        if (period == "all")
        {
            var row = await QueryAllAsync(connection, username);
            return new GetTaskSummaryResult([row]);
        }

        var fragments = PeriodFragments.TryGetValue(period, out var f)
            ? f : PeriodFragments["monthly"];

        var from = query.From ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var to = query.To ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var sql = $"""
            SELECT {fragments.SelectPeriod} AS Period,
                   SUM(CASE WHEN Bucket = 'NotStarted' THEN 1 ELSE 0 END) AS NotStarted,
                   SUM(CASE WHEN Bucket = 'InProgress' THEN 1 ELSE 0 END) AS InProgress,
                   SUM(CASE WHEN Bucket = 'Overdue'    THEN 1 ELSE 0 END) AS Overdue,
                   SUM(CASE WHEN Bucket = 'Completed'  THEN 1 ELSE 0 END) AS Completed
            FROM workflow.vw_UserTaskSummary
            WHERE Username = @Username
              AND EventAt >= @From
              AND EventAt <  @ToExclusive
            GROUP BY {fragments.GroupBy}
            ORDER BY {fragments.GroupBy}
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);
        parameters.Add("From", from.ToDateTime(TimeOnly.MinValue));
        parameters.Add("ToExclusive", to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var rows = await connection.QueryAsync<TaskSummaryDto>(sql, parameters);
        return new GetTaskSummaryResult(rows.ToList());
    }

    private static async Task<TaskSummaryDto> QueryAllAsync(
        System.Data.IDbConnection connection, string username)
    {
        const string sql = """
            SELECT SUM(CASE WHEN Bucket = 'NotStarted' THEN 1 ELSE 0 END) AS NotStarted,
                   SUM(CASE WHEN Bucket = 'InProgress' THEN 1 ELSE 0 END) AS InProgress,
                   SUM(CASE WHEN Bucket = 'Overdue'    THEN 1 ELSE 0 END) AS Overdue,
                   SUM(CASE WHEN Bucket = 'Completed'  THEN 1 ELSE 0 END) AS Completed
            FROM workflow.vw_UserTaskSummary
            WHERE Username = @Username
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);

        var row = await connection.QuerySingleOrDefaultAsync<TaskSummaryDto>(sql, parameters);
        return row is null
            ? new TaskSummaryDto { Period = "All" }
            : row with { Period = "All" };
    }
}
