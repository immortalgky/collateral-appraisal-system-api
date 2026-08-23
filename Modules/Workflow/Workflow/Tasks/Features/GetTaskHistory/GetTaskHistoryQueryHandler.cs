using Auth.Contracts.Users;
using Dapper;
using Shared.Data;

namespace Workflow.Tasks.Features.GetTaskHistory;

public class GetTaskHistoryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IUserLookupService userLookupService
) : IQueryHandler<GetTaskHistoryQuery, GetTaskHistoryResponse>
{
    // Resolve the workflow instance's correlation id (string column on WorkflowInstances)
    // and use it to fetch CompletedTasks; PendingTasks can be filtered directly by
    // WorkflowInstanceId since that column exists on the pending side.
    private const string Sql = """
        DECLARE @CorrelationGuid uniqueidentifier = (
            SELECT TRY_CAST(CorrelationId AS uniqueidentifier)
            FROM workflow.WorkflowInstances
            WHERE Id = @WorkflowInstanceId
        );

        -- Wrapped in a derived table because SQL Server only allows plain output-column
        -- references in a UNION's ORDER BY, not expressions.
        SELECT history.* FROM (
            SELECT
                Id            AS TaskId,
                TaskName,
                TaskDescription,
                AssignedTo,
                AssignedType,
                AssignedAt,
                AssigneeAssignedAt,
                OpenedAt,
                TaskStatus                  AS TaskState,
                SlaStartAt,
                DueAt,
                SlaStatus,
                SlaDurationHours,
                CAST(NULL AS datetime2)     AS CompletedAt,
                CAST(NULL AS nvarchar(10))  AS ActionTaken,
                Movement,
                CAST(NULL AS nvarchar(1000)) AS Remark
            FROM workflow.PendingTasks
            WHERE WorkflowInstanceId = @WorkflowInstanceId

            UNION ALL

            SELECT
                Id          AS TaskId,
                TaskName,
                TaskDescription,
                AssignedTo,
                AssignedType,
                AssignedAt,
                AssigneeAssignedAt,
                OpenedAt,
                TaskStatus                  AS TaskState,
                SlaStartAt,
                DueAt,
                SlaStatus,
                SlaDurationHours,
                CompletedAt,
                ActionTaken,
                Movement,
                Remark
            FROM workflow.CompletedTasks
            WHERE @CorrelationGuid IS NOT NULL AND CorrelationId = @CorrelationGuid
        ) history
        -- Order on the per-holder stamp, not AssignedAt: a supervisor reassign deliberately freezes
        -- AssignedAt across the outgoing audit row and the incoming holder's row, so AssignedAt alone
        -- ties and SQL Server is free to return them in either order. CompletedAt breaks any residual
        -- tie (genuinely simultaneous fan-out tasks), pending rows sorting last.
        -- TaskId last so the order is a TOTAL order: two rows can genuinely share both stamps
        -- (simultaneous fan-out items, or several unopened pending rows), and without this the
        -- engine stays free to swap them between runs, drifting the displayed sequence numbers.
        -- Chronology is already settled by the two keys above; this one only has to be stable.
        ORDER BY history.AssigneeAssignedAt,
                 CASE WHEN history.CompletedAt IS NULL THEN 1 ELSE 0 END,
                 history.CompletedAt,
                 history.TaskId;
        """;

    public async Task<GetTaskHistoryResponse> Handle(
        GetTaskHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        var rows = (await connection.QueryAsync<TaskHistoryRow>(
            Sql,
            new { query.WorkflowInstanceId })).ToList();

        // AssignedType "1" = single user; "2" = pool/group. Only resolve display names for "1".
        const string userAssignedType = "1";

        var userAssignees = rows
            .Where(r => r.AssignedType == userAssignedType)
            .Select(r => r.AssignedTo)
            .ToArray();

        var userMap = await userLookupService.GetByUsernamesAsync(userAssignees, cancellationToken);

        var items = rows.Select(r =>
        {
            string? firstName = null;
            string? lastName = null;
            string? displayName = null;

            if (r.AssignedType == userAssignedType)
            {
                if (userMap.TryGetValue(r.AssignedTo, out var user))
                {
                    firstName = user.FirstName;
                    lastName = user.LastName;
                    displayName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                        displayName = r.AssignedTo;
                }
                else
                {
                    displayName = r.AssignedTo;
                }
            }

            return new TaskHistoryItemDto
            {
                TaskId = r.TaskId,
                TaskName = r.TaskName,
                TaskDescription = r.TaskDescription,
                AssignedTo = r.AssignedTo,
                AssignedToFirstName = firstName,
                AssignedToLastName = lastName,
                AssignedToDisplayName = displayName,
                AssignedType = r.AssignedType,
                AssignedAt = r.AssignedAt,
                AssigneeAssignedAt = r.AssigneeAssignedAt,
                OpenedAt = r.OpenedAt,
                TaskState = r.TaskState,
                SlaStartAt = r.SlaStartAt,
                DueAt = r.DueAt,
                SlaStatus = r.SlaStatus,
                SlaDurationHours = r.SlaDurationHours,
                CompletedAt = r.CompletedAt,
                ActionTaken = r.ActionTaken,
                Movement = r.Movement,
                Remark = r.Remark,
            };
        }).ToList();

        return new GetTaskHistoryResponse(items);
    }

    private sealed class TaskHistoryRow
    {
        public Guid TaskId { get; set; }
        public string TaskName { get; set; } = default!;
        public string? TaskDescription { get; set; }
        public string AssignedTo { get; set; } = default!;
        public string AssignedType { get; set; } = default!;
        public DateTime AssignedAt { get; set; }
        public DateTime AssigneeAssignedAt { get; set; }
        public DateTime? OpenedAt { get; set; }
        public string? TaskState { get; set; }
        public DateTime? SlaStartAt { get; set; }
        public DateTime? DueAt { get; set; }
        public string? SlaStatus { get; set; }
        public int? SlaDurationHours { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ActionTaken { get; set; }
        public string Movement { get; set; } = "F";
        public string? Remark { get; set; }
    }
}
