namespace Shared.Messaging.Events;

public record DocumentLinkedIntegrationEvent : IntegrationEvent
{
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public List<Guid> Documents = [];
}
