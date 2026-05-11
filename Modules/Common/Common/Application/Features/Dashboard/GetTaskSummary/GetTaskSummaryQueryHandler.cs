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

        // Default to the trailing 7 days (today - 7 .. today) when no range is supplied,
        // preserving the original "this week" feel while allowing callers to override.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var from = query.From ?? today.AddDays(-7);
        var to = query.To ?? today;

        // vw_UserTaskSummary exposes EventAt for all buckets:
        //   active/overdue tasks → EventAt = AssignedAt
        //   completed tasks      → EventAt = CompletedAt
        // We filter every bucket by EventAt so the cohort is consistent with the
        // period the caller chose. "Overdue" is an overlapping flag row (same task
        // can appear in both NotStarted/InProgress AND Overdue), so it is counted
        // separately as per the existing view design.
        const string sql = """
            SELECT
                SUM(CASE WHEN Bucket = 'NotStarted' THEN 1 ELSE 0 END) AS NotStarted,
                SUM(CASE WHEN Bucket = 'InProgress' THEN 1 ELSE 0 END) AS InProgress,
                SUM(CASE WHEN Bucket = 'Overdue'    THEN 1 ELSE 0 END) AS Overdue,
                SUM(CASE WHEN Bucket = 'Completed'  THEN 1 ELSE 0 END) AS Completed
            FROM workflow.vw_UserTaskSummary
            WHERE Username = @Username
              AND EventAt >= @From
              AND EventAt <  DATEADD(day, 1, @To)
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);
        parameters.Add("From", from.ToDateTime(TimeOnly.MinValue));
        parameters.Add("To", to.ToDateTime(TimeOnly.MinValue));

        var connection = connectionFactory.GetOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<GetTaskSummaryResult>(sql, parameters);

        return row ?? new GetTaskSummaryResult(0, 0, 0, 0);
    }
}
