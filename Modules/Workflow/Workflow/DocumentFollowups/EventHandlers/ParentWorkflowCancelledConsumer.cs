using Shared.Messaging.Filters;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Events;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.EventHandlers;

/// <summary>
/// Cascades cancellation: when a parent appraisal workflow is cancelled, all of its
/// open document followups are auto-cancelled with a system reason.
/// </summary>
public class ParentWorkflowCancelledConsumer(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    IPublisher publisher,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<ParentWorkflowCancelledConsumer> logger)
    : IConsumer<WorkflowCancelled>
{
    public async Task Consume(ConsumeContext<WorkflowCancelled> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        var openFollowups = await dbContext.DocumentFollowups
            .Where(f => f.RaisingWorkflowInstanceId == msg.WorkflowInstanceId
                        && f.Status == DocumentFollowupStatus.Open)
            .ToListAsync(context.CancellationToken);

        if (openFollowups.Count == 0)
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        const string systemReason = "Parent appraisal cancelled";

        foreach (var followup in openFollowups)
        {
            try
            {
                followup.Cancel(systemReason);
                await dbContext.SaveChangesAsync(context.CancellationToken);

                foreach (var ev in followup.ClearDomainEvents())
                    await publisher.Publish(ev, context.CancellationToken);

                if (followup.FollowupWorkflowInstanceId.HasValue)
                {
                    await workflowService.CancelWorkflowAsync(
                        followup.FollowupWorkflowInstanceId.Value,
                        "system",
                        systemReason,
                        context.CancellationToken);
                }

                logger.LogInformation(
                    "Cascade-cancelled document followup {FollowupId} due to parent {ParentId} cancellation",
                    followup.Id, msg.WorkflowInstanceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to cascade-cancel document followup {FollowupId} for parent {ParentId}",
                    followup.Id, msg.WorkflowInstanceId);
            }
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
