namespace Shared.Messaging.Events;

/// <summary>
/// Round-trips the outcome of the async LOS PMA delivery (published by the Integration module's
/// WebhookDispatchConsumer after attempting all of a property's title deliveries) back to the
/// Appraisal module, so <c>AppraisalProperty.ExternalSyncStatus</c> reflects the real delivery
/// outcome instead of staying at "Pending" forever.
/// </summary>
public record PmaExternalSyncStatusChangedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid PropertyId { get; init; }
    public string Status { get; init; } = default!;
    public string? Error { get; init; }
}

/// <summary>
/// String values for <see cref="PmaExternalSyncStatusChangedIntegrationEvent.Status"/>, shared by
/// the Integration (publisher) and Appraisal (subscriber) modules so both sides use the exact same
/// literals without either module taking a compile-time dependency on the other's domain types.
/// Mirrors three of the four values on <c>AppraisalProperty.ExternalSyncStatus</c> — Pending is set
/// locally by the Appraisal module on PMA save and never round-tripped through this event.
/// </summary>
public static class PmaExternalSyncStatus
{
    /// <summary>Internal appraisal — the request has no ExternalSystem, so there is nothing to sync.</summary>
    public const string NotSynced = "NotSynced";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}
