using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.Data;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.LockTask;

public class LockTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IWorkflowNotificationService notificationService,
    ILogger<LockTaskCommandHandler> logger
) : ICommandHandler<LockTaskCommand, LockTaskResult>
{
    public async Task<LockTaskResult> Handle(LockTaskCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new LockTaskResult(false, ErrorMessage: "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new LockTaskResult(false, ErrorMessage: "Task not found");

        if (task.AssignedType != "2")
            return new LockTaskResult(false, ErrorMessage: "Only pool tasks can be locked");

        if (task.WorkingBy != null && task.WorkingBy != username)
            return new LockTaskResult(false, ErrorMessage: $"Task is locked by {task.WorkingBy}");

        var poolGroup = task.AssignedTo;
        task.Lock(username);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request acquired the lock concurrently — reload to get the actual holder
            await dbContext.Entry(task).ReloadAsync(cancellationToken);
            return new LockTaskResult(false, ErrorMessage: $"Task is locked by {task.WorkingBy}");
        }

        logger.LogInformation("User {Username} locked pool task {TaskId} in pool {PoolGroup}",
            username, command.TaskId, poolGroup);

        await notificationService.NotifyPoolTaskLocked(poolGroup, command.TaskId, username);

        return new LockTaskResult(true, LockedBy: username, LockedAt: task.LockedAt);
    }
}
