namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module via outbox when a FeeAppointmentApproval transitions to Resolved.
/// Consumed by the Appraisal module to apply per-component outcomes to Appointment / AppraisalFeeItem.
/// </summary>
public class FeeAppointmentApprovalResolvedIntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid ApprovalId { get; init; }
    public IReadOnlyList<FeeApprovalLineOutcome> LineOutcomes { get; init; } = [];

    /// <summary>
    /// The bank code (e.g. "P5229", = AspNetUsers.UserName) of the user who resolved the approval.
    /// Used by the Appraisal module to stamp the real approver on the Appointment / AppraisalFeeItem
    /// instead of a system placeholder. Null only for system-initiated resolutions.
    /// </summary>
    public string? ResolvedByCode { get; init; }

    public DateTime OccurredOn { get; set; }
}

public record FeeApprovalLineOutcome(
    string LineType,
    Guid TargetId,
    /// <summary>"Approved" or "Rejected"</summary>
    string Decision,
    string? Reason);
