using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Identity;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Validates that the current user is the assignee of the pending task for this appraisal.
/// Uses Dapper for a lightweight read query against workflow.PendingTasks.
/// </summary>
public class ValidateTaskOwnershipStep(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    ILogger<ValidateTaskOwnershipStep> logger) : IActivityProcessStep
{
    public string Name => "ValidateTaskOwnership";

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        try
        {
            using var connection = connectionFactory.GetOpenConnection();

            // Query ALL pending tasks for this CorrelationId (supports multi-task approval scenarios)
            var tasks = await connection.QueryAsync<TaskOwnershipDto>(
                """
                SELECT AssignedTo, AssignedType, WorkingBy
                FROM workflow.PendingTasks
                WHERE CorrelationId = @AppraisalId
                """,
                new { context.AppraisalId });

            var taskList = tasks.ToList();
            if (taskList.Count == 0)
            {
                logger.LogWarning(
                    "No pending task found for appraisal {AppraisalId}", context.AppraisalId);
                return ProcessStepResult.Fail("No pending task found for this appraisal");
            }

            var username = currentUserService.Username;
            var isOwner = taskList.Any(task => task.AssignedType == "2"
                // Pool task: check if the user has claimed it (WorkingBy matches)
                ? string.Equals(task.WorkingBy, username, StringComparison.OrdinalIgnoreCase)
                // Individual task: check if assigned directly to this user
                : string.Equals(task.AssignedTo, username, StringComparison.OrdinalIgnoreCase));

            if (!isOwner)
            {
                logger.LogWarning(
                    "User {Username} attempted to complete task for appraisal {AppraisalId} but is not assigned to any of {TaskCount} pending tasks",
                    username,
                    context.AppraisalId,
                    taskList.Count);
                return ProcessStepResult.Fail("You are not authorized to complete this task");
            }

            return ProcessStepResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to validate task ownership for appraisal {AppraisalId}", context.AppraisalId);
            return ProcessStepResult.Fail(ex.Message);
        }
    }

    private sealed record TaskOwnershipDto(string AssignedTo, string AssignedType, string? WorkingBy);
}
