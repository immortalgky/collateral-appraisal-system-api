using Shared.Time;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.ExpireOverdueFanOutTasks;

/// <summary>
/// 1. Finds all PendingTasks for (WorkflowInstanceId, ActivityId) whose DueAt &lt;= now.
/// 2. Archives each one as CompletedTask with ActionTaken="Expired".
/// 3. Calls ResumeWorkflowAsync so FanOutTaskActivity re-evaluates its all-terminal gate.
///
/// Idempotent: PendingTask rows are deleted on archive, so a second run finds nothing and exits cleanly.
/// </summary>
public class ExpireOverdueFanOutTasksCommandHandler(
    IAssignmentRepository assignmentRepository,
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

        var expiredCompanyIds = new List<Guid>();

        foreach (var pt in overdue)
        {
            var completed = CompletedTask.CreateFromPendingTask(pt, "Expired", now);
            await assignmentRepository.AddCompletedTaskAsync(completed, cancellationToken);
            await assignmentRepository.RemovePendingTaskAsync(pt, cancellationToken);

            if (pt.AssigneeCompanyId.HasValue)
                expiredCompanyIds.Add(pt.AssigneeCompanyId.Value);

            logger.LogInformation(
                "ExpireOverdueFanOutTasks: archived PendingTask {TaskId} (company {CompanyId}) as Expired for workflow {WorkflowInstanceId}",
                pt.Id, pt.AssigneeCompanyId, command.WorkflowInstanceId);
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
}
