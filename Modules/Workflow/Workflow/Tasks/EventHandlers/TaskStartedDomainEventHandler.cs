using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskStartedDomainEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<TaskStartedDomainEventHandler> logger
) : INotificationHandler<TaskStartedDomainEvent>
{
    public async Task Handle(TaskStartedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling TaskStartedDomainEvent for CorrelationId {CorrelationId}, AssignedTo {AssignedTo}",
            notification.CorrelationId, notification.AssignedTo);

        await publishEndpoint.Publish(new TaskStartedIntegrationEvent
        {
            CorrelationId = notification.CorrelationId,
            AssignedTo = notification.AssignedTo,
            AssignedAt = notification.AssignedAt,
            PreviousAssignedTo = notification.PreviousAssignedTo
        }, cancellationToken);
    }
}
