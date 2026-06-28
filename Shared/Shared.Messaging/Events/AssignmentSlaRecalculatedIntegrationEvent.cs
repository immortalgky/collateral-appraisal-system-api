namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module after it recomputes an appointment-anchored group-window
/// deadline in response to <see cref="AppointmentDateChangedIntegrationEvent"/>.
///
/// Consumed by the Appraisal module to update
/// <c>AppraisalAssignment.SLADueDate</c> (via <c>RecalculateSlaDueDate</c>) so the
/// persisted assignment-level deadline stays aligned with the rescheduled appointment.
/// </summary>
public class AssignmentSlaRecalculatedIntegrationEvent
{
    /// <summary>Appraisal whose assignment deadline changed.</summary>
    public Guid AppraisalId { get; init; }

    /// <summary>The specific assignment whose SLADueDate must be re-stamped.</summary>
    public Guid AssignmentId { get; init; }

    /// <summary>
    /// The new SLA due-date computed from the rescheduled appointment.
    /// Local-kind (Asia/Bangkok, matches GETDATE() in SQL views).
    /// </summary>
    public DateTime NewSlaDueDate { get; init; }

    public DateTime OccurredOn { get; init; }
}
