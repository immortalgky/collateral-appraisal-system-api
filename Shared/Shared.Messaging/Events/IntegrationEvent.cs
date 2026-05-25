using Shared.Data.Outbox;

namespace Shared.Messaging.Events;

public record IntegrationEvent : IHasOccurredOn
{
    // Computed once at construction — expression-bodied would generate a new value on every access
    public Guid EventId { get; } = Guid.CreateVersion7();
    // Default to DateTime.MinValue; IntegrationEventOutbox.Publish stamps ApplicationNow at the publish boundary.
    public DateTime OccurredOn { get; set; }
    public string EventType => GetType().AssemblyQualifiedName!;
}
