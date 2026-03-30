using Dapper;
using Shared.Data;
using Shared.Exceptions;
using Shared.Identity;

namespace Workflow.Tasks.Features.GetTaskById;

public class GetTaskByIdQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetTaskByIdQuery, TaskDetailResult>
{
    private const string Sql = """
        SELECT
            Id                              AS TaskId,
            CorrelationId                   AS AppraisalId,
            WorkflowInstanceId,
            ActivityId,
            AssignedTo                      AS AssigneeUserId,
            AssignedType,
            CAST(TaskName AS nvarchar(100)) AS TaskName,
            TaskDescription
        FROM workflow.PendingTasks
        WHERE Id = @TaskId
        """;

    public async Task<TaskDetailResult> Handle(
        GetTaskByIdQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        var dto = await connection.QuerySingleOrDefaultAsync<TaskDetailDto>(
            Sql,
            new { query.TaskId });

        if (dto is null)
            throw new NotFoundException(nameof(TaskDetailResult), query.TaskId);

        var isOwner = string.Equals(
            dto.AssigneeUserId,
            currentUserService.Username,
            StringComparison.OrdinalIgnoreCase);

        return new TaskDetailResult
        {
            TaskId = dto.TaskId,
            AppraisalId = dto.AppraisalId,
            WorkflowInstanceId = dto.WorkflowInstanceId,
            ActivityId = dto.ActivityId,
            AssigneeUserId = dto.AssigneeUserId,
            AssignedType = dto.AssignedType,
            TaskName = dto.TaskName,
            TaskDescription = dto.TaskDescription,
            IsOwner = isOwner
        };
    }

    private sealed record TaskDetailDto(
        Guid TaskId,
        Guid AppraisalId,
        Guid WorkflowInstanceId,
        string ActivityId,
        string AssigneeUserId,
        string AssignedType,
        string? TaskName,
        string? TaskDescription);
}
