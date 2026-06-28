namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Appraisal module whenever an appointment date is confirmed or changed
/// (initial creation / auto-approved reschedule / bank-approved reschedule).
///
/// Consumed by the Workflow module to:
///   (a) write <c>appointmentDate</c> into <c>WorkflowInstance.Variables</c> so the next
///       TaskActivity execution can use it as the SLA anchor, and
///   (b) recompute <c>PendingTask.DueAt</c> for any currently-active appointment-anchored task.
/// </summary>
public class AppointmentDateChangedIntegrationEvent
{
    /// <summary>Appraisal the appointment belongs to.</summary>
    public Guid AppraisalId { get; init; }

    /// <summary>
    /// Workflow correlation ID (= Appraisal.RequestId = Request.Id). Used to find the
    /// WorkflowInstance that must receive the updated variable.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Assignment whose appointment changed.</summary>
    public Guid AssignmentId { get; init; }

    /// <summary>Confirmed appointment date (local time, Asia/Bangkok convention).</summary>
    public DateTime AppointmentDate { get; init; }

    public DateTime OccurredOn { get; init; }
}
