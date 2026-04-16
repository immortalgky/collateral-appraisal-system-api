using Shared.Identity;
using Workflow.Data;

namespace Workflow.Tasks.Features.LockTask;

public class HeartbeatTaskLockCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<HeartbeatTaskLockCommandHandler> logger
) : ICommandHandler<HeartbeatTaskLockCommand, HeartbeatTaskLockResult>
{
    public async Task<HeartbeatTaskLockResult> Handle(HeartbeatTaskLockCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new HeartbeatTaskLockResult(false, ErrorMessage: "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new HeartbeatTaskLockResult(false, ErrorMessage: "Task not found");

        if (!task.IsLockedBy(username))
            return new HeartbeatTaskLockResult(false, ErrorMessage: "You do not hold the lock on this task");

        // Re-lock to refresh LockedAt timestamp
        task.Lock(username);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug("User {Username} refreshed lock heartbeat on task {TaskId}", username, command.TaskId);

        return new HeartbeatTaskLockResult(true);
    }
}
