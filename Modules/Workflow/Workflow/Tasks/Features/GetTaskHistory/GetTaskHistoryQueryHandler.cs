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

        SELECT
            Id            AS TaskId,
            TaskName,
            TaskDescription,
            AssignedTo,
            AssignedType,
            AssignedAt,
            CAST(NULL AS datetime2)     AS CompletedAt,
            CAST(NULL AS nvarchar(10))  AS ActionTaken,
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
            CompletedAt,
            ActionTaken,
            Remark
        FROM workflow.CompletedTasks
        WHERE @CorrelationGuid IS NOT NULL
          AND CorrelationId = @CorrelationGuid

        ORDER BY AssignedAt;
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
                CompletedAt = r.CompletedAt,
                ActionTaken = r.ActionTaken,
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
        public DateTime? CompletedAt { get; set; }
        public string? ActionTaken { get; set; }
        public string? Remark { get; set; }
    }
}
