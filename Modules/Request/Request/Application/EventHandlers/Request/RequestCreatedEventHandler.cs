using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class RequestCreatedEventHandler(ILogger<RequestCreatedEventHandler> logger, IBus bus)
    : INotificationHandler<RequestCreatedEvent>
{
    public async Task Handle(RequestCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);

        var integrationEvent = new RequestCreatedIntegrationEvent
        {
            RequestId = notification.Request.Id
        };

        await bus.Publish(integrationEvent, cancellationToken);
    }
}