using Request.Domain.RequestTitles.Events;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Request.Application.EventHandlers.Request;

/// <summary>
/// When a document is attached to a title, emit the same request-scoped integration event
/// that request-level uploads emit. Downstream consumers (e.g. the document-followup
/// auto-fulfill consumer) match on RequestId + DocumentType and don't care where the file
/// was attached — so title-level uploads must be visible to them.
/// </summary>
public class TitleDocumentAttachedEventHandler(
    IIntegrationEventOutbox outbox,
    ILogger<TitleDocumentAttachedEventHandler> logger)
    : INotificationHandler<TitleDocumentAttachedEvent>
{
    public Task Handle(TitleDocumentAttachedEvent notification, CancellationToken cancellationToken)
    {
        var doc = notification.TitleDocument;

        if (!doc.DocumentId.HasValue || string.IsNullOrWhiteSpace(doc.DocumentType))
        {
            // Placeholder rows (no file yet) — nothing to broadcast.
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Title document linked: RequestId={RequestId} TitleId={TitleId} DocumentId={DocumentId} DocumentType={DocumentType}",
            notification.RequestId, doc.TitleId, doc.DocumentId, doc.DocumentType);

        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(
                notification.RequestId, doc.DocumentId.Value, doc.DocumentType),
            correlationId: notification.RequestId.ToString());

        return Task.CompletedTask;
    }
}
