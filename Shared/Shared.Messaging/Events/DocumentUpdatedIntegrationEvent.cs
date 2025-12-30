namespace Shared.Messaging.Events;

public record DocumentUpdatedIntegrationEvent(Guid RequestId, Guid PreviousDocumentId, Guid DocumentId)
    : IntegrationEvent;