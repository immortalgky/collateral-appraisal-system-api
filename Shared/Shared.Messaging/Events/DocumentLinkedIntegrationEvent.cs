namespace Shared.Messaging.Events;

public record DocumentLinkedIntegrationEvent : IntegrationEvent
{
    public Guid SessionId { get; set; }
    public List<DocumentLink> Documents { get; set; } = [];
}

public record DocumentLink
{
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public Guid? DocumentId { get; set; }
    public bool IsUnlink { get; set; } = false;
}

