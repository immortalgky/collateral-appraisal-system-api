namespace Shared.Messaging.Events;

public record SessionCompletedIntegrationEvent(Guid SessionId, Guid RequestId) : IntegrationEvent;
