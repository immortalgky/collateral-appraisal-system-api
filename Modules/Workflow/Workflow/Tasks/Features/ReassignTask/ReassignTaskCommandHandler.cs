using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Shared.Time;
using Workflow.Services.Groups;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.AssigneeSelection.Pipeline;

namespace Workflow.Tasks.Features.ReassignTask;

public class ReassignTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IGroupMonitoringService groupMonitoringService,
    IWorkflowInstanceRepository instanceRepository,
    IAssignmentPipeline assignmentPipeline,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReassignTaskCommandHandler> logger
) : ICommandHandler<ReassignTaskCommand, ReassignTaskResult>
{
    public async Task<ReassignTaskResult> Handle(ReassignTaskCommand command, CancellationToken cancellationToken)
    {
        // 1. Resolve current user
        var currentUser = currentUserService.Username;
        if (string.IsNullOrEmpty(currentUser))
            return new ReassignTaskResult(false, ErrorMessage: "User not authenticated");

        // 2. Target user required
        if (string.IsNullOrWhiteSpace(command.NewAssignedTo))
            return new ReassignTaskResult(false, ErrorMessage: "Target user is required");

        // 3. Task exists
        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new ReassignTaskResult(false, ErrorMessage: "Task not found");

        // 4. Only person-assigned tasks (AssignedType == "1")
        if (task.AssignedType != "1")
            return new ReassignTaskResult(false, ErrorMessage: "Only person-assigned tasks can be reassigned (use ClaimTask for pool tasks)");

        // 5. Task must be in Assigned or InProgress
        var assignedStatus = TaskStatus.Assigned;
        var inProgressStatus = TaskStatus.InProgress;
        if (task.TaskStatus != assignedStatus && task.TaskStatus != inProgressStatus)
            return new ReassignTaskResult(false, ErrorMessage: "Task is not in a reassignable state");

        // 6. Idempotency: same assignee is a no-op
        if (string.Equals(task.AssignedTo, command.NewAssignedTo, StringComparison.OrdinalIgnoreCase))
            return new ReassignTaskResult(true, Changed: false, AssignedTo: task.AssignedTo);

        // 7. Supervisor must monitor the current assignee's group
        var isSupervisor = await groupMonitoringService.IsUserSupervisedByAsync(
            task.AssignedTo, currentUser, cancellationToken);
        if (!isSupervisor)
            return new ReassignTaskResult(false, ErrorMessage: "You do not supervise this user");

        // 8. Target must be eligible for the activity
        var isEligible = await IsNewAssigneeEligibleAsync(task, command.NewAssignedTo, cancellationToken);
        if (!isEligible)
            return new ReassignTaskResult(false, ErrorMessage: "Target user is not eligible for this activity");

        // Snapshot the current state as an audit row BEFORE mutating the aggregate.
        // Use CreateAuditFromPendingTask (fresh Id) so the PendingTask row stays alive
        // and a future normal completion can still insert its own CompletedTask row
        // without a PK collision.
        var auditRow = CompletedTask.CreateAuditFromPendingTask(task, "Reassigned", dateTimeProvider.ApplicationNow);
        dbContext.CompletedTasks.Add(auditRow);

        // Mutate — raises PendingTaskReassignedDomainEvent (fired pre-save by interceptor)
        task.Reassign(command.NewAssignedTo, "1", raiseEventFor: "supervisor");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ReassignTaskResult(false, ErrorMessage: "Task was modified by another user; please refresh");
        }

        logger.LogInformation(
            "Supervisor {Supervisor} reassigned task {TaskId} from {Previous} to {New}",
            currentUser, command.TaskId, auditRow.AssignedTo, command.NewAssignedTo);

        return new ReassignTaskResult(true, Changed: true, AssignedTo: command.NewAssignedTo);
    }

    private async Task<bool> IsNewAssigneeEligibleAsync(
        PendingTask task, string newAssignedTo, CancellationToken cancellationToken)
    {
        var instance = await instanceRepository.GetByIdAsync(task.WorkflowInstanceId, cancellationToken);
        if (instance is null)
        {
            logger.LogWarning(
                "WorkflowInstance {InstanceId} not found when checking eligibility for task {TaskId}",
                task.WorkflowInstanceId, task.Id);
            return false;
        }

        var properties = ActivityPropertiesExtractor.Extract(instance, task.ActivityId, logger);

        var activityContext = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = task.ActivityId,
            Properties = properties,
            Variables = instance.Variables,
            InputData = new Dictionary<string, object>(),
            CurrentAssignee = instance.CurrentAssignee,
            CancellationToken = cancellationToken,
            WorkflowInstance = instance
        };

        var pipelineCtx = await assignmentPipeline.GetEligibleAssigneesAsync(activityContext, cancellationToken);

        return pipelineCtx.CandidatePool.Any(m =>
            string.Equals(m.UserId, newAssignedTo, StringComparison.OrdinalIgnoreCase));
    }

}
