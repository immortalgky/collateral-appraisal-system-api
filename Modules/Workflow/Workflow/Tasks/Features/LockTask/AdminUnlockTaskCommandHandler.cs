using Workflow.Data;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.LockTask;

public class AdminUnlockTaskCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowNotificationService notificationService,
    ILogger<AdminUnlockTaskCommandHandler> logger
) : ICommandHandler<AdminUnlockTaskCommand, AdminUnlockTaskResult>
{
    public async Task<AdminUnlockTaskResult> Handle(AdminUnlockTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new AdminUnlockTaskResult(false, ErrorMessage: "Task not found");

        if (task.AssignedType != "2")
            return new AdminUnlockTaskResult(false, ErrorMessage: "Only pool tasks can be unlocked via admin");

        var poolGroup = task.AssignedTo;
        var previousHolder = task.WorkingBy;
        task.ReleaseLock();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin released lock on pool task {TaskId} (was held by {PreviousHolder}) in pool {PoolGroup}",
            command.TaskId, previousHolder ?? "nobody", poolGroup);

        await notificationService.NotifyPoolTaskUnlocked(poolGroup, command.TaskId, "admin");

        return new AdminUnlockTaskResult(true);
    }
}
