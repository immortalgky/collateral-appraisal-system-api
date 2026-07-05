using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class DocumentUnlinkedEventHandler(IIntegrationEventOutbox outbox, ILogger<DocumentUnlinkedEventHandler> logger)
    : INotificationHandler<DocumentUnlinkedEvent>
{
    public Task Handle(DocumentUnlinkedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Document with ID {DocumentId} unlinked from PricingAnalysis with ID {PricingAnalysisId}",
            notification.DocumentId, notification.PricingId);

        outbox.Publish(
            new DocumentUnlinkedIntegrationEvent(notification.PricingId, notification.DocumentId),
            correlationId: notification.PricingId.ToString());

        return Task.CompletedTask;
    }
}
