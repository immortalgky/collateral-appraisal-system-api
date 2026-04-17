using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.GetReminders;

public class GetRemindersQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetRemindersQuery, GetRemindersResponse>
{
    public async Task<GetRemindersResponse> Handle(
        GetRemindersQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetRemindersResponse(new List<ReminderDto>());

        const string sql = """
            SELECT TOP 10
                Id,
                Type,
                Title,
                AppraisalNumber,
                DueAt,
                Overdue
            FROM (
                -- Source 1: tasks assigned to me that are overdue or due within 24 h
                SELECT
                    tl.Id                                       AS Id,
                    'task_due'                                  AS Type,
                    COALESCE(
                        tl.TaskDescription,
                        CAST(tl.TaskType AS nvarchar(200))
                    )                                           AS Title,
                    COALESCE(tl.AppraisalNumber, tl.RequestNumber, CONCAT('REQ-', LEFT(CAST(tl.RequestId AS nvarchar(36)), 8))) AS AppraisalNumber,
                    CAST(tl.DueAt AS datetimeoffset)            AS DueAt,
                    CASE WHEN tl.DueAt < GETUTCDATE() THEN 1 ELSE 0 END AS Overdue
                FROM workflow.vw_TaskList tl
                WHERE tl.AssigneeUserId = @Username
                  AND tl.PendingTaskStatus != 'Completed'
                  AND tl.DueAt         IS NOT NULL
                  AND tl.DueAt         <= DATEADD(HOUR, 24, GETUTCDATE())

                UNION ALL

                -- Source 2: open document follow-ups raised by me
                SELECT
                    df.Id                                       AS Id,
                    'followup'                                  AS Type,
                    'Document follow-up'                        AS Title,
                    COALESCE(a.AppraisalNumber, CAST(df.AppraisalId AS nvarchar(50)))
                                                                AS AppraisalNumber,
                    CAST(df.RaisedAt AS datetimeoffset)         AS DueAt,
                    0                                           AS Overdue
                FROM workflow.DocumentFollowups df
                LEFT JOIN appraisal.Appraisals a ON a.Id = df.AppraisalId
                WHERE df.RaisingUserId = @Username
                  AND df.Status        = 'Open'
            ) src
            ORDER BY Overdue DESC, DueAt ASC
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<ReminderRow>(sql, parameters);

        var items = rows.Select(r => new ReminderDto(
            r.Id,
            r.Type,
            r.Title,
            r.AppraisalNumber,
            r.DueAt,
            r.Overdue != 0)).ToList();

        return new GetRemindersResponse(items);
    }

    private sealed class ReminderRow
    {
        public Guid Id { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? AppraisalNumber { get; init; }
        public DateTimeOffset DueAt { get; init; }
        public int Overdue { get; init; }
    }
}
