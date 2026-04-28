namespace Shared.Messaging.Events;

/// <summary>
/// Published when an admin adds an appraisal to an existing Draft quotation.
/// Consumed by: FE real-time refresh (admin page re-fetches quotation).
/// </summary>
public record AppraisalAddedToQuotationIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid AppraisalId { get; init; }
    public Guid AdminUserId { get; init; }
}
