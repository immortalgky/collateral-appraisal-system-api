namespace Shared.Messaging.Events;

public record DocumentUnlinkedIntegrationEvent(Guid RequestId, Guid DocumentId) : IntegrationEvent;