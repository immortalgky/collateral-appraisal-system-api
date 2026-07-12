namespace Shared.Messaging.Events;

/// <summary>
/// Raised after a PMA (Land &amp; Building or Condo) property save commits, so the outbox can push
/// the updated PMA data to the external LOS system asynchronously. Carries only the identifiers —
/// the actual PMA data (prices, titles, condo fields) is looked up at delivery time by the
/// Integration module's WebhookDispatchConsumer, not stuffed into the event.
/// </summary>
public record AppraisalPmaUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid PropertyId { get; init; }
}
