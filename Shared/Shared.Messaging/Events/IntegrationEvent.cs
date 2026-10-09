using Shared.Data.Outbox;

namespace Shared.Messaging.Events;

public record IntegrationEvent : IHasOccurredOn
{
    // Computed once at construction — expression-bodied would generate a new value on every access.
    //
    // `init` is load-bearing: without a setter System.Text.Json cannot write the property, so every
    // deserialization (the outbox delivery service, then MassTransit at the consumer) silently ran the
    // initializer again and minted a NEW id. The value in the outbox payload was discarded, and the
    // `eventId` in the outbound webhook envelope identified the *delivery* rather than the event —
    // so a redelivery of the same notification arrived with a different id and could not be deduplicated
    // by the receiving system, which is the only thing that field is there for.
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    // Default to DateTime.MinValue; IntegrationEventOutbox.Publish stamps ApplicationNow at the publish boundary.
    public DateTime OccurredOn { get; set; }
    public string EventType => GetType().AssemblyQualifiedName!;
}
