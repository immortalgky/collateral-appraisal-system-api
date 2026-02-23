namespace Shared.Messaging.Events;

public record IntegrationEvent
{
    public Guid EventId => Guid.CreateVersion7();
    public DateTime OccurredOn => DateTime.Now;
    public string EventType => GetType().AssemblyQualifiedName;
}