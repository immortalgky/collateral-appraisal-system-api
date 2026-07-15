using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class DocumentUpdatedEventHandler(IIntegrationEventOutbox outbox, ILogger<DocumentUpdatedEventHandler> logger)
    : INotificationHandler<DocumentUpdatedEvent>
{
    public Task Handle(DocumentUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Document with ID {DocumentId} replaced previous document {PreviousDocumentId} on PricingAnalysis with ID {PricingAnalysisId}",
            notification.DocumentId, notification.PreviousDocumentId, notification.PricingId);

        outbox.Publish(
            new DocumentUpdatedIntegrationEvent(
                notification.PricingId, notification.PreviousDocumentId, notification.DocumentId),
            correlationId: notification.PricingId.ToString());

        return Task.CompletedTask;
    }
}
