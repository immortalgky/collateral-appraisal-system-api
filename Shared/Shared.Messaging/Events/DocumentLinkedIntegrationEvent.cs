namespace Shared.Messaging.Events;

public record DocumentLinkedIntegrationEvent : IntegrationEvent
{
    public Guid SessionId { get; init; }
    public List<DocumentLink> DocumentLinks { get; init; } = [];
}

public record DocumentLink
{
    public string EntityType { get; init; } = default!;
    public Guid EntityId { get; init; } 
    public Guid DocumentId { get; init; }
    public bool IsUnlink { get; init; } = false;

}
