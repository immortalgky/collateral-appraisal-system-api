using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Identity;
using Workflow.Data.Entities;

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
    public sealed record Parameters;

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "ValidateTaskOwnership",
        displayName: "Validate Task Ownership",
        kind: StepKind.Validation,
        description: "Ensures the completing user is the assigned owner (or has claimed the pool task).");

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        try
        {
            using var connection = connectionFactory.GetOpenConnection();

            var tasks = await connection.QueryAsync<TaskOwnershipDto>(
                """
                SELECT AssignedTo, AssignedType, WorkingBy
                FROM workflow.PendingTasks
                WHERE CorrelationId = @CorrelationId
                """,
                new { ctx.CorrelationId });

            var taskList = tasks.ToList();
            if (taskList.Count == 0)
            {
                logger.LogWarning(
                    "No pending task found for correlation {CorrelationId}", ctx.CorrelationId);
                return ProcessStepResult.Fail("TASK_NOT_FOUND", "No pending task found for this request");
            }

            var username = currentUserService.Username;
            var isOwner = taskList.Any(task => task.AssignedType == "2"
                ? string.Equals(task.WorkingBy, username, StringComparison.OrdinalIgnoreCase)
                : string.Equals(task.AssignedTo, username, StringComparison.OrdinalIgnoreCase));

            if (!isOwner)
            {
                logger.LogWarning(
                    "User {Username} attempted to complete task for correlation {CorrelationId} but is not assigned",
                    username, ctx.CorrelationId);
                return ProcessStepResult.Fail("NOT_TASK_OWNER", "You are not authorized to complete this task");
            }

            return ProcessStepResult.Pass();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to validate task ownership for correlation {CorrelationId}", ctx.CorrelationId);
            return ProcessStepResult.Error(ex);
        }
    }

    private sealed record TaskOwnershipDto(string AssignedTo, string AssignedType, string? WorkingBy);
}
