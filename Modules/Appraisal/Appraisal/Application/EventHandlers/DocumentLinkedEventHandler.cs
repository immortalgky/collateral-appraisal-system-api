using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class DocumentLinkedEventHandler(IIntegrationEventOutbox outbox, ILogger<DocumentLinkedEventHandler> logger)
    : INotificationHandler<DocumentLinkedEvent>
{
    public Task Handle(DocumentLinkedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Document with ID {DocumentId} linked to PricingAnalysis with ID {PricingAnalysisId}",
            notification.DocumentId, notification.PricingId);

        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(
                notification.PricingId, notification.DocumentId),
            correlationId: notification.PricingId.ToString());

        return Task.CompletedTask;
    }
}
