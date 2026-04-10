using Shared.Identity;
using Workflow.Data;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.LockTask;

public class UnlockTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IWorkflowNotificationService notificationService,
    ILogger<UnlockTaskCommandHandler> logger
) : ICommandHandler<UnlockTaskCommand, UnlockTaskResult>
{
    public async Task<UnlockTaskResult> Handle(UnlockTaskCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new UnlockTaskResult(false, ErrorMessage: "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new UnlockTaskResult(false, ErrorMessage: "Task not found");

        if (!task.IsLockedBy(username))
            return new UnlockTaskResult(false, ErrorMessage: "You do not hold the lock on this task");

        var poolGroup = task.AssignedTo;
        task.ReleaseLock();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} released lock on pool task {TaskId} in pool {PoolGroup}",
            username, command.TaskId, poolGroup);

        await notificationService.NotifyPoolTaskUnlocked(poolGroup, command.TaskId, "user");

        return new UnlockTaskResult(true);
    }
}
