namespace Shared.Messaging.Events;

/// <summary>
/// Notification signal for the admin who finalized a quotation, emitted by the Appraisal module and
/// consumed by the Notification module, which sends it as <c>QuotationAutoAssignIncomplete</c>.
/// </summary>
public record QuotationAutoAssignNotificationIntegrationEvent
{
    public Guid QuotationRequestId { get; init; }

    /// <summary>User code of the finalizer.</summary>
    public string Recipient { get; init; } = default!;

    public string Title { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string ActionUrl { get; init; } = default!;
}
