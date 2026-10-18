namespace Shared.Messaging.Services;

/// <summary>
/// Leading text of the <c>Error</c> that <see cref="IntegrationEventDeliveryService{TDbContext}"/> stores
/// when it fails an outbox row deterministically. Named so a reader that needs to classify a Failed row
/// (the Failed Messages screen) matches on these constants instead of repeating the literals.
/// </summary>
public static class OutboxFailureReasons
{
    /// <summary>The event type resolved but is outside the allowed namespace.</summary>
    public const string Disallowed = "Disallowed type:";

    /// <summary>The event type could not be resolved, and the version-skew grace period has passed.</summary>
    public const string Unresolvable = "Unresolvable type:";

    /// <summary>The payload threw while deserialising, and the version-skew grace period has passed.</summary>
    public const string DeserializationFailed = "Deserialization failed:";

    /// <summary>The payload deserialised to null, and the version-skew grace period has passed.</summary>
    public const string DeserializationReturnedNull = "Deserialization returned null";
}
