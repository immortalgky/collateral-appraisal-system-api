using MassTransit;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class DocumentLinkedEventHandler(IBus bus, ILogger<DocumentLinkedEventHandler> logger)
    : INotificationHandler<DocumentLinkedEvent>
{
    public async Task Handle(DocumentLinkedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Document with ID {DocumentId} linked to Request with ID {RequestId}",
            notification.DocumentId, notification.RequestId);

        await bus.Publish(new DocumentLinkedIntegrationEventV2(notification.RequestId, notification.DocumentId),
            cancellationToken);
    }
}