namespace Shared.Messaging.Events;

public record QuotationReadyIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; init; }
    public string? RequestNumber { get; init; }
    public string? ExternalCaseKey { get; init; }
    public Guid QuotationId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime ReadyAt { get; init; }
}
