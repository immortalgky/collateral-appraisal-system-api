using MassTransit;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class DocumentUnlinkedEventHandler(IBus bus, ILogger<DocumentUnlinkedEventHandler> logger)
    : INotificationHandler<DocumentUnlinkedEvent>
{
    public async Task Handle(DocumentUnlinkedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Document with ID {DocumentId} unlinked from Request with ID {RequestId}",
            notification.DocumentId, notification.RequestId);

        await bus.Publish(new DocumentUnlinkedIntegrationEvent(notification.RequestId, notification.DocumentId),
            cancellationToken);
    }
}