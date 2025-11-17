namespace Shared.Messaging.Events;

public record DocumentUnLinkedIntegrationEvent
{
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public List<Guid> Documents = [];
}
