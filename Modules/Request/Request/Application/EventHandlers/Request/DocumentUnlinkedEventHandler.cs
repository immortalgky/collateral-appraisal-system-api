using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class DocumentUnlinkedEventHandler(IIntegrationEventOutbox outbox, ILogger<DocumentUnlinkedEventHandler> logger)
    : INotificationHandler<DocumentUnlinkedEvent>
{
    public Task Handle(DocumentUnlinkedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Document with ID {DocumentId} unlinked from Request with ID {RequestId}",
            notification.DocumentId, notification.RequestId);

        outbox.Publish(new DocumentUnlinkedIntegrationEvent(notification.RequestId, notification.DocumentId),
            correlationId: notification.RequestId.ToString());

        return Task.CompletedTask;
    }
}