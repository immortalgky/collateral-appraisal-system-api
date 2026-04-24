namespace Shared.Messaging.Events;

/// <summary>
/// Published when a quotation's submission window closes (auto or admin-triggered).
/// Triggers notification to the admin pool to begin reviewing bids.
/// </summary>
public record QuotationSubmissionsClosedIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid RequestId { get; init; }

    /// <summary>
    /// Optional list of specific admin user IDs to notify.
    /// If empty, the Notification handler should broadcast to the admin group.
    /// </summary>
    public string[] AdminUserIds { get; init; } = [];
}
