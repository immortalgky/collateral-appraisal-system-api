using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Shared.Messaging.Events;
using Workflow.Data;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.ClaimTask;

public class ClaimTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IWorkflowNotificationService notificationService,
    IPublishEndpoint publishEndpoint,
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

        var isPoolMember = currentUserService.Roles.Any(r =>
            string.Equals(r, task.AssignedTo, StringComparison.OrdinalIgnoreCase));
        if (!isPoolMember)
            return new ClaimTaskResult(false, ErrorMessage: "You are not a member of this pool");

        // Capture pool group before reassignment for notification
        var poolGroup = task.AssignedTo;

        // Claim: reassign from pool to specific person
        task.Reassign(username, "1");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another user claimed this task at the same moment
            return new ClaimTaskResult(false, ErrorMessage: "Task was already claimed by another user");
        }

        logger.LogInformation("User {Username} claimed pool task {TaskId} from pool {PoolGroup}",
            username, command.TaskId, poolGroup);

        // Push real-time notification to pool members
        await notificationService.NotifyPoolTaskClaimed(poolGroup, command.TaskId, username);

        // Publish integration event so dashboard counters transfer from pool group to individual
        await publishEndpoint.Publish(new TaskClaimedIntegrationEvent
        {
            CorrelationId = task.CorrelationId,
            PoolGroup = poolGroup,
            ClaimedBy = username,
            AssignedAt = task.AssignedAt
        }, cancellationToken);

        return new ClaimTaskResult(true, AssignedTo: username);
    }
}
