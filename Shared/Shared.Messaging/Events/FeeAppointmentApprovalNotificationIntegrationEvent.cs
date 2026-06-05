namespace Shared.Messaging.Events;

/// <summary>
/// Cross-module notification signal for FeeAppointmentApproval state transitions.
/// Emitted by the Workflow module and consumed by the Notification module.
/// Types: FeeAppointmentApprovalRaised, FeeAppointmentApprovalCancelled.
/// </summary>
public record FeeAppointmentApprovalNotificationIntegrationEvent
{
    public string Type { get; init; } = default!;
    public Guid ApprovalId { get; init; }
    public Guid AppraisalId { get; init; }
    public Guid? FollowupWorkflowInstanceId { get; init; }

    /// <summary>User or group who should receive the notification.</summary>
    public string? Recipient { get; init; }

    public string Title { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? Reason { get; init; }
}
