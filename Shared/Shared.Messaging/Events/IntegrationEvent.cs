namespace Shared.Messaging.Events;

public record IntegrationEvent
{
    // Computed once at construction — expression-bodied would generate a new value on every access
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType => GetType().AssemblyQualifiedName!;
}
