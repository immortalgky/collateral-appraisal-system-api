using System.Text.Json;
using Shared.Time;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.ExpireOverdueFanOutTasks;

/// <summary>
/// 1. Finds all PendingTasks for (WorkflowInstanceId, ActivityId) whose DueAt &lt;= now.
/// 2. Archives each one as CompletedTask with ActionTaken from <c>onTimeout.complete</c> (if declared)
///    or "Cancelled" (default) when the activity has a stage definition, otherwise "Expired" (legacy).
/// 3. Calls ResumeWorkflowAsync so FanOutTaskActivity re-evaluates its all-terminal gate.
///
/// Idempotent: PendingTask rows are deleted on archive, so a second run finds nothing and exits cleanly.
/// </summary>
public class ExpireOverdueFanOutTasksCommandHandler(
    IAssignmentRepository assignmentRepository,
    IWorkflowInstanceRepository instanceRepository,
    IWorkflowService workflowService,
    IDateTimeProvider dateTimeProvider,
    ILogger<ExpireOverdueFanOutTasksCommandHandler> logger)
    : ICommandHandler<ExpireOverdueFanOutTasksCommand, ExpireOverdueFanOutTasksResult>
{
    public async Task<ExpireOverdueFanOutTasksResult> Handle(
        ExpireOverdueFanOutTasksCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;

        // Load all remaining PendingTasks for this fan-out activity
        var pending = await assignmentRepository.GetFanOutPendingTasksAsync(
            command.WorkflowInstanceId, command.ActivityId, cancellationToken);

        // Keep only those whose deadline has passed
        var overdue = pending.Where(pt => pt.DueAt.HasValue && pt.DueAt.Value <= now).ToList();

        if (overdue.Count == 0)
        {
            logger.LogDebug(
                "ExpireOverdueFanOutTasks: no overdue tasks for workflow {WorkflowInstanceId} activity {ActivityId}",
                command.WorkflowInstanceId, command.ActivityId);
            return new ExpireOverdueFanOutTasksResult(0, []);
        }

        // Load the instance once so we can both (a) read onTimeout.complete and (b) close
        // the per-fan-out-item stage history for each overdue task on the same execution.
        var instance = await instanceRepository.GetWithExecutionsAsync(
            command.WorkflowInstanceId, cancellationToken);

        var actionTakenForTimeout = ResolveTimeoutOutcomeFromInstance(
            instance, command.ActivityId);

        // The activity execution that owns the fan-out items for this activity id.
        var execution = instance?.ActivityExecutions
            .FirstOrDefault(e => e.ActivityId == command.ActivityId
                                 && e.Status == ActivityExecutionStatus.InProgress);

        var expiredCompanyIds = new List<Guid>();

        foreach (var pt in overdue)
        {
            var completed = CompletedTask.CreateFromPendingTask(pt, actionTakenForTimeout, now);
            await assignmentRepository.AddCompletedTaskAsync(completed, cancellationToken);
            await assignmentRepository.RemovePendingTaskAsync(pt, cancellationToken);

            // Close the per-item stage history so audit/excludeAssigneesFrom queries see a
            // consistent ExitedOn for timed-out items (matching the action-driven path).
            if (execution is not null && pt.AssigneeCompanyId.HasValue)
                execution.CompleteFanOutItem(pt.AssigneeCompanyId.Value, dateTimeProvider);

            if (pt.AssigneeCompanyId.HasValue)
                expiredCompanyIds.Add(pt.AssigneeCompanyId.Value);

            logger.LogInformation(
                "ExpireOverdueFanOutTasks: archived PendingTask {TaskId} (company {CompanyId}) as '{ActionTaken}' for workflow {WorkflowInstanceId}",
                pt.Id, pt.AssigneeCompanyId, actionTakenForTimeout, command.WorkflowInstanceId);
        }

        // Check if any non-overdue tasks are still pending; if all are now gone, resume the workflow.
        // ResumeActivityAsync will re-check GetFanOutPendingTasksAsync and transition if empty.
        var stillPending = await assignmentRepository.GetFanOutPendingTasksAsync(
            command.WorkflowInstanceId, command.ActivityId, cancellationToken);

        if (stillPending.Count == 0)
        {
            logger.LogInformation(
                "ExpireOverdueFanOutTasks: all tasks terminal for workflow {WorkflowInstanceId} activity {ActivityId} — resuming workflow",
                command.WorkflowInstanceId, command.ActivityId);

            await workflowService.ResumeWorkflowAsync(
                command.WorkflowInstanceId,
                command.ActivityId,
                "SYSTEM",
                new Dictionary<string, object>
                {
                    ["decisionTaken"] = "auto-expired",
                    ["completedBy"] = "SYSTEM"
                },
                cancellationToken: cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "ExpireOverdueFanOutTasks: {StillPending} task(s) still pending for workflow {WorkflowInstanceId} — workflow stays paused",
                stillPending.Count, command.WorkflowInstanceId);
        }

        return new ExpireOverdueFanOutTasksResult(overdue.Count, expiredCompanyIds.AsReadOnly());
    }

    /// <summary>
    /// Reads <c>onTimeout.complete</c> from the activity's JSON definition.
    /// Falls back to "Expired" when the activity has no <c>onTimeout</c> property (legacy behavior).
    /// </summary>
    private string ResolveTimeoutOutcomeFromInstance(
        WorkflowInstance? instance,
        string activityId)
    {
        try
        {
            var jsonDef = instance?.WorkflowDefinition?.JsonDefinition;
            if (string.IsNullOrEmpty(jsonDef))
                return "Expired";

            using var doc = JsonDocument.Parse(jsonDef);
            var root = doc.RootElement;

            if (!root.TryGetProperty("activities", out var activities) ||
                activities.ValueKind != JsonValueKind.Array)
                return "Expired";

            foreach (var activity in activities.EnumerateArray())
            {
                if (!activity.TryGetProperty("id", out var idProp) ||
                    idProp.GetString() != activityId)
                    continue;

                if (!activity.TryGetProperty("properties", out var props))
                    return "Expired";

                if (!props.TryGetProperty("onTimeout", out var onTimeout) ||
                    onTimeout.ValueKind != JsonValueKind.Object)
                    return "Expired";

                if (onTimeout.TryGetProperty("complete", out var completeProp) &&
                    completeProp.ValueKind == JsonValueKind.String)
                    return completeProp.GetString() ?? "Cancelled";

                return "Cancelled"; // onTimeout present but no complete key → default
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "ExpireOverdueFanOutTasks: failed to read onTimeout for activity {ActivityId}, using 'Expired'",
                activityId);
        }

        return "Expired";
    }
}
