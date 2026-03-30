using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class DocumentLinkedEventHandler(IIntegrationEventOutbox outbox, ILogger<DocumentLinkedEventHandler> logger)
    : INotificationHandler<DocumentLinkedEvent>
{
    public Task Handle(DocumentLinkedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Document with ID {DocumentId} linked to Request with ID {RequestId}",
            notification.DocumentId, notification.RequestId);

        outbox.Publish(new DocumentLinkedIntegrationEventV2(notification.RequestId, notification.DocumentId),
            correlationId: notification.RequestId.ToString());

        return Task.CompletedTask;
    }
}