using MassTransit;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

public class DocumentUpdatedEventHandler(IBus bus, ILogger<DocumentUpdatedEventHandler> logger)
    : INotificationHandler<DocumentUpdatedEvent>
{
    public async Task Handle(DocumentUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("DocumentUpdatedEvent handled for DocumentId: {DocumentId}", notification.DocumentId);

        await bus.Publish(
            new DocumentUpdatedIntegrationEvent(notification.RequestId, notification.PreviousDocumentId,
                notification.DocumentId), cancellationToken);
    }
}