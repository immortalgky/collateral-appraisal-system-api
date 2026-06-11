namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Appraisal module (via outbox in SendQuotationCommandHandler) when
/// a quotation is sent to an external company. Consumed by the Notification module which
/// renders an HTML template and delivers it via SMTP.
/// </summary>
public record QuotationSentEmailIntegrationEvent : IntegrationEvent
{
    /// <summary>The quotation request that triggered this send — used as the send-log ReferenceId.</summary>
    public Guid QuotationRequestId { get; init; }

    public string? To { get; init; }
    public string? Cc { get; init; }
    public string? Bcc { get; init; }
    public string Subject { get; init; } = default!;
    public string? Content { get; init; }
}
