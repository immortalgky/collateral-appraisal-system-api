using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Assignment.Assignments.EventHandlers;

public class AssignmentCreatedEventHandler(ILogger<AssignmentCreatedEventHandler> logger, IBus bus)
    : INotificationHandler<AssignmentCreatedEvent>
{
    public async Task Handle(AssignmentCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);

        var integrationEvent = new RequestCreatedIntegrationEvent
        {
            RequestId = notification.Assignment.Id
        };

        await bus.Publish(integrationEvent, cancellationToken);
    }
}