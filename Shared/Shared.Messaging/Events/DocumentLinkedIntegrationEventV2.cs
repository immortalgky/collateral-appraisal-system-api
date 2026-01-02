namespace Shared.Messaging.Events;

public record DocumentLinkedIntegrationEventV2(Guid RequestId, Guid DocumentId) : IntegrationEvent;