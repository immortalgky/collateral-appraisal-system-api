using Shared.Identity;
using Workflow.Data;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.ClaimTask;

public class ClaimTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IWorkflowNotificationService notificationService,
    ILogger<ClaimTaskCommandHandler> logger
) : ICommandHandler<ClaimTaskCommand, ClaimTaskResult>
{
    public async Task<ClaimTaskResult> Handle(ClaimTaskCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new ClaimTaskResult(false, ErrorMessage: "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new ClaimTaskResult(false, ErrorMessage: "Task not found");

        if (task.AssignedType != "2")
            return new ClaimTaskResult(false, ErrorMessage: "Only pool tasks can be claimed");

        // Capture pool group before reassignment for notification
        var poolGroup = task.AssignedTo;

        // Claim: reassign from pool to specific person
        task.Reassign(username, "1", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} claimed pool task {TaskId} from pool {PoolGroup}",
            username, command.TaskId, poolGroup);

        // Push real-time notification to pool members
        await notificationService.NotifyPoolTaskClaimed(poolGroup, command.TaskId, username);

        return new ClaimTaskResult(true, AssignedTo: username);
    }
}
