namespace Shared.Messaging.Events;

/// <summary>
/// Published when a quotation's submission window closes (auto or admin-triggered).
/// Triggers notification to all IntAdmin users to begin reviewing bids.
/// </summary>
public record QuotationSubmissionsClosedIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid RequestId { get; init; }
}
