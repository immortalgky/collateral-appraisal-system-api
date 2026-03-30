using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class DocumentUpdatedEventHandler(IIntegrationEventOutbox outbox, ILogger<DocumentUpdatedEventHandler> logger)
    : INotificationHandler<DocumentUpdatedEvent>
{
    public Task Handle(DocumentUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("DocumentUpdatedEvent handled for DocumentId: {DocumentId}", notification.DocumentId);

        outbox.Publish(
            new DocumentUpdatedIntegrationEvent(notification.RequestId, notification.PreviousDocumentId,
                notification.DocumentId),
            correlationId: notification.RequestId.ToString());

        return Task.CompletedTask;
    }
}