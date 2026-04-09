namespace Shared.Messaging.Events;

/// <summary>
/// Cross-module notification signal for document followup state transitions.
/// Emitted by the Workflow module and consumed by the Notification module
/// which forwards it to the SignalR NotificationHub. The <see cref="Type"/>
/// string is the identifier the frontend listens for:
/// <c>DocumentFollowupRaised</c>, <c>DocumentFollowupResolved</c>,
/// <c>DocumentFollowupCancelled</c>, <c>DocumentLineItemDeclined</c>.
/// </summary>
public record DocumentFollowupNotificationIntegrationEvent
{
    public string Type { get; init; } = default!;
    public Guid FollowupId { get; init; }
    public Guid RaisingTaskId { get; init; }
    public Guid ParentAppraisalId { get; init; }
    public Guid? FollowupWorkflowInstanceId { get; init; }

    /// <summary>User who should receive the notification.</summary>
    public string Recipient { get; init; } = default!;

    public string Title { get; init; } = default!;
    public string Message { get; init; } = default!;

    /// <summary>Optional reason (for Cancelled / Declined).</summary>
    public string? Reason { get; init; }
}
