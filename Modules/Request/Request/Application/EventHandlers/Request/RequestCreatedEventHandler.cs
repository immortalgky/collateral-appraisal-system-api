using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class RequestCreatedEventHandler(ILogger<RequestCreatedEventHandler> logger, IIntegrationEventOutbox outbox)
    : INotificationHandler<RequestCreatedEvent>
{
    public Task Handle(RequestCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);

        outbox.Publish(new RequestCreatedIntegrationEvent
        {
            RequestId = notification.Request.Id
        }, correlationId: notification.Request.Id.ToString());

        return Task.CompletedTask;
    }
}