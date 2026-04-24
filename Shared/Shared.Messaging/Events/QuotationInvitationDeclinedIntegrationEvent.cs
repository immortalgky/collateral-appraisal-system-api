namespace Shared.Messaging.Events;

/// <summary>
/// Published when an ext-company user declines a quotation invitation.
/// Consumed by: Notification module (inform admin).
/// </summary>
public record QuotationInvitationDeclinedIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid CompanyId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
