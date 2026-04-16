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
            return new GetTaskSummaryResult(0, 0, 0, 0);

        // ISO week starts on Monday.  DATEADD(wk, DATEDIFF(wk, 0, GETUTCDATE()), 0)
        // gives Monday 00:00:00 UTC of the current week because SQL Server week-0
        // anchor (1900-01-01) is a Monday.
        const string sql = """
            SELECT
                SUM(CASE WHEN Bucket = 'NotStarted'  THEN 1 ELSE 0 END) AS NotStarted,
                SUM(CASE WHEN Bucket = 'InProgress'  THEN 1 ELSE 0 END) AS InProgress,
                SUM(CASE WHEN Bucket = 'Overdue'     THEN 1 ELSE 0 END) AS Overdue,
                SUM(CASE WHEN Bucket = 'Completed'
                          AND EventAt >= DATEADD(wk, DATEDIFF(wk, 0, GETUTCDATE()), 0)
                         THEN 1 ELSE 0 END)                              AS CompletedThisWeek
            FROM workflow.vw_UserTaskSummary
            WHERE Username = @Username
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);

        var connection = connectionFactory.GetOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<GetTaskSummaryResult>(sql, parameters);

        return row ?? new GetTaskSummaryResult(0, 0, 0, 0);
    }
}
