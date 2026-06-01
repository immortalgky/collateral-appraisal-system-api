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
    public DateTime OccurredOn { get; set; }
}

public record FeeApprovalLineOutcome(
    string LineType,
    Guid TargetId,
    /// <summary>"Approved" or "Rejected"</summary>
    string Decision,
    string? Reason);
