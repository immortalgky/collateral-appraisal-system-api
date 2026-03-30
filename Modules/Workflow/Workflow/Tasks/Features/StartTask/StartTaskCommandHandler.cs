using Shared.Identity;
using Workflow.Data;
using Workflow.Tasks.ValueObjects;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.StartTask;

public class StartTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IWorkflowNotificationService notificationService,
    ILogger<StartTaskCommandHandler> logger
) : ICommandHandler<StartTaskCommand, StartTaskResult>
{
    public async Task<StartTaskResult> Handle(StartTaskCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new StartTaskResult(false, "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new StartTaskResult(false, "Task not found");

        if (task.TaskStatus == TaskStatus.InProgress)
            return new StartTaskResult(false, $"Task is already being worked on by {task.WorkingBy}");

        task.StartWorking(username);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} started working on task {TaskId}", username, command.TaskId);

        // Push real-time notification for pool tasks
        if (task.AssignedType == "2")
        {
            await notificationService.NotifyPoolTaskStarted(
                task.AssignedTo, command.TaskId, username);
        }

        return new StartTaskResult(true);
    }
}
