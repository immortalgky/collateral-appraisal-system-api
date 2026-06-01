namespace Workflow.Contracts.FeeAppointmentApprovals;

/// <summary>
/// Raises a FeeAppointmentApproval request for components that require approval.
/// Sent by the Appraisal module via the integration-event outbox after evaluating
/// which components (appointment change / fee additions) need bank approval.
/// </summary>
public record RaiseFeeAppointmentApprovalCommand(
    Guid AppraisalId,
    string RequestSource,
    IReadOnlyList<RaiseFeeApprovalLineDto> Lines)
    : ICommand<RaiseFeeAppointmentApprovalResult>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record RaiseFeeApprovalLineDto(
    string LineType,             // "Appointment" | "Fee"
    Guid TargetId,               // AppointmentId or FeeItemId
    DateTime? NewDate,           // appointment change: the proposed new date
    int? RescheduleCount,        // appointment change: current reschedule count after Reschedule()
    string? FeeCode,
    string? FeeDescription,
    decimal? FeeAmount);

public record RaiseFeeAppointmentApprovalResult(Guid ApprovalId, Guid FollowupWorkflowInstanceId);
