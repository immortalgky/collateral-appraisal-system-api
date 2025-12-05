namespace Shared.Messaging.Events;

public record RequestCreatedIntegrationEvent : IntegrationEvent
{
    public long RequestId { get; set; } = default!;
}