namespace Shared.Messaging.Events;

/// <summary>
/// Published when an admin removes an appraisal from a Draft quotation.
/// If the removal auto-cancelled the Draft, the quotation status will be Cancelled when consumers read it.
/// Consumed by: FE real-time refresh; unlocks the assignment screen for the removed appraisal.
/// </summary>
public record AppraisalRemovedFromQuotationIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid AppraisalId { get; init; }
    public Guid AdminUserId { get; init; }
}
