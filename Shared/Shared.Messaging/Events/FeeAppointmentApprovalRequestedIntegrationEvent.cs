namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Appraisal module when an external company submits a fee/appointment
/// change that requires bank approval. Consumed by the Workflow module which creates the
/// FeeAppointmentApproval aggregate and spawns the approval child workflow.
/// </summary>
public class FeeAppointmentApprovalRequestedIntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string RequestSource { get; init; } = "Ext";
    public IReadOnlyList<FeeApprovalRequestedLineDto> Lines { get; init; } = [];
    public DateTime OccurredOn { get; set; }
}

public record FeeApprovalRequestedLineDto(
    string LineType,       // "Appointment" | "Fee"
    Guid TargetId,
    DateTime? NewDate,
    int? RescheduleCount,
    string? FeeCode,
    string? FeeDescription,
    decimal? FeeAmount);
