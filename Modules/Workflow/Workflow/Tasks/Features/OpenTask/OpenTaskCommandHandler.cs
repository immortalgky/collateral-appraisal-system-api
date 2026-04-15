using Dapper;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Identity;
using Workflow.Data;
using Workflow.Tasks.ValueObjects;

namespace Workflow.Tasks.Features.OpenTask;

public class OpenTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    ISqlConnectionFactory connectionFactory,
    ILogger<OpenTaskCommandHandler> logger
) : ICommandHandler<OpenTaskCommand, OpenTaskResult>
{
    public async Task<OpenTaskResult> Handle(OpenTaskCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new OpenTaskResult(false, "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new OpenTaskResult(false, "Task not found");

        var isOwner = task.AssignedType == "1" &&
            string.Equals(task.AssignedTo, username, StringComparison.OrdinalIgnoreCase);
        var isPoolMember = task.AssignedType == "2" &&
            currentUserService.Roles.Any(r => string.Equals(r, task.AssignedTo, StringComparison.OrdinalIgnoreCase));

        if (!isOwner && !isPoolMember)
            return new OpenTaskResult(false, "You are not assigned to this task");

        // For personal tasks: transition to InProgress on first open
        if (isOwner && task.TaskStatus != TaskStatus.InProgress)
        {
            task.StartWorking(username);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new OpenTaskResult(false, "Task was already opened by another session");
            }

            logger.LogInformation("User {Username} opened task {TaskId}", username, task.Id);
        }

        // Pool tasks: no state change — user views/edits without claiming.
        // Use POST /tasks/{taskId}/lock to acquire editing rights,
        // and POST /tasks/{taskId}/claim to permanently move the task to personal list.

        // Fetch redirect data — same Dapper pattern as GetTaskByIdQueryHandler
        var connection = connectionFactory.GetOpenConnection();
        var appraisalId = await connection.ExecuteScalarAsync<Guid?>(
            """
            SELECT TOP 1 Id FROM appraisal.Appraisals
            WHERE RequestId = @CorrelationId
            ORDER BY CreatedAt DESC
            """,
            new { task.CorrelationId });

        return new OpenTaskResult(
            IsSuccess: true,
            AppraisalId: appraisalId,
            WorkflowInstanceId: task.WorkflowInstanceId,
            TaskName: task.TaskName
        );
    }
}
