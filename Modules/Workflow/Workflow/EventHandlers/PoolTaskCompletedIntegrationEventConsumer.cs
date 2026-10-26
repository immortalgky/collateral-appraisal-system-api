using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

/// <summary>
/// Tells the remaining members of a pool that a task was completed and left their list.
/// Idempotent and best-effort: a failed push is logged, never retried or dead-lettered.
/// </summary>
public class PoolTaskCompletedIntegrationEventConsumer(
    IWorkflowNotificationService notificationService,
    ILogger<PoolTaskCompletedIntegrationEventConsumer> logger)
    : IConsumer<PoolTaskCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PoolTaskCompletedIntegrationEvent> context)
    {
        var message = context.Message;
        try
        {
            await notificationService.NotifyPoolTaskCompleted(message.PoolGroup, message.TaskId, message.CompletedBy);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push pool-task-completed notification for task {TaskId} in pool {PoolGroup}",
                message.TaskId, message.PoolGroup);
        }
    }
}
