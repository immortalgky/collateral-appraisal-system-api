using MassTransit;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Consumes FeeAppointmentApprovalResolvedIntegrationEvent from the Workflow module and applies
/// per-component decisions to the Appraisal domain:
/// - Appointment line: Approve → Appointment.Approve() | Reject → Appointment.RejectReschedule()
/// - Fee line:         Approve → AppraisalFeeItem.Approve() | Reject → AppraisalFeeItem.Reject()
///
/// When an Appointment line is Approved, publishes AppointmentDateChangedIntegrationEvent so the
/// Workflow module can update WorkflowInstance.Variables and recompute PendingTask.DueAt for any
/// appointment-anchored SLA activity.
///
/// InboxGuard prevents double-application on MassTransit retries.
/// Domain method calls are guarded against non-Pending status so retries are idempotent.
/// </summary>
public class FeeAppointmentApprovalResolvedIntegrationEventHandler(
    AppraisalDbContext dbContext,
    InboxGuard<AppraisalDbContext> inboxGuard,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    ILogger<FeeAppointmentApprovalResolvedIntegrationEventHandler> logger)
    : IConsumer<FeeAppointmentApprovalResolvedIntegrationEvent>
{
    private const string SystemActor = "system";

    public async Task Consume(
        ConsumeContext<FeeAppointmentApprovalResolvedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        logger.LogInformation(
            "Consuming FeeAppointmentApprovalResolved for appraisal {AppraisalId}, {Count} line outcomes",
            msg.AppraisalId, msg.LineOutcomes.Count);

        // Resolve the appraisal's workflow correlation ID once — needed for the appointment event.
        var correlationId = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == msg.AppraisalId)
            .Select(a => a.RequestId)
            .FirstOrDefaultAsync(context.CancellationToken);

        foreach (var outcome in msg.LineOutcomes)
        {
            if (outcome.LineType == "Appointment")
                await ApplyAppointmentOutcomeAsync(outcome, msg.AppraisalId, correlationId, msg.ResolvedByCode, context.CancellationToken);
            else if (outcome.LineType == "Fee")
                await ApplyFeeOutcomeAsync(outcome, msg.ResolvedByCode, context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }

    private async Task ApplyAppointmentOutcomeAsync(
        FeeApprovalLineOutcome outcome,
        Guid appraisalId,
        Guid correlationId,
        string? resolvedByCode,
        CancellationToken ct)
    {
        var appointment = await dbContext.Appointments
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == outcome.TargetId, ct);

        if (appointment is null)
        {
            logger.LogWarning("Appointment {Id} not found for fee approval outcome — skipping", outcome.TargetId);
            return;
        }

        // Stamp the real resolving user's bank code (matches Appointment.ApprovedBy/ChangedBy).
        // Falls back to the system actor only when the resolution was system-initiated.
        var actor = resolvedByCode ?? SystemActor;

        if (outcome.Decision == "Approved")
        {
            // Idempotent: only call Approve when actually Pending
            if (appointment.Status == "Pending")
            {
                appointment.Approve(actor);
                logger.LogInformation("Appointment {Id} approved via FeeAppointmentApproval", outcome.TargetId);

                // Notify Workflow module: update Variables["appointmentDate"] and recompute
                // PendingTask.DueAt for any appointment-anchored SLA activity.
                outbox.Publish(new AppointmentDateChangedIntegrationEvent
                {
                    AppraisalId = appraisalId,
                    CorrelationId = correlationId,
                    AssignmentId = appointment.AssignmentId,
                    AppointmentDate = appointment.AppointmentDateTime,
                    OccurredOn = dateTimeProvider.ApplicationNow
                });
            }
            else
            {
                logger.LogInformation("Appointment {Id} already in status '{Status}', skipping approve", outcome.TargetId, appointment.Status);
            }
        }
        else
        {
            // Idempotent: only revert when status is Pending (after a Reschedule)
            if (appointment.Status == "Pending")
            {
                appointment.RejectReschedule(actor, outcome.Reason);
                logger.LogInformation("Appointment {Id} reschedule rejected via FeeAppointmentApproval", outcome.TargetId);
            }
            else
            {
                logger.LogInformation("Appointment {Id} already in status '{Status}', skipping reject-reschedule", outcome.TargetId, appointment.Status);
            }
        }

        // Always clear the approval markers once resolved (approve OR reject), regardless of whether
        // the status transition was applied. Otherwise an outcome that arrives when the appointment
        // is no longer Pending (e.g. redelivery, or a status change via another path) would leave
        // ApprovalSubmittedAt set, permanently blocking cancel/reschedule via the edit-lock guards.
        // (Approve() already clears them in the Pending case; this makes the two branches symmetric.)
        appointment.ClearApprovalMarkers();
    }

    private async Task ApplyFeeOutcomeAsync(FeeApprovalLineOutcome outcome, string? resolvedByCode, CancellationToken ct)
    {
        var feeItem = await dbContext.AppraisalFeeItems
            .FirstOrDefaultAsync(i => i.Id == outcome.TargetId, ct);

        if (feeItem is null)
        {
            logger.LogWarning("AppraisalFeeItem {Id} not found for fee approval outcome — skipping", outcome.TargetId);
            return;
        }

        // Stamp the real resolving user's bank code. Falls back to the system actor only for
        // system-initiated resolutions.
        var approver = resolvedByCode ?? SystemActor;

        if (outcome.Decision == "Approved")
        {
            // Idempotent: only Approve when still Pending
            if (feeItem.ApprovalStatus == "Pending")
            {
                feeItem.Approve(approver);
                logger.LogInformation("FeeItem {Id} approved via FeeAppointmentApproval", outcome.TargetId);
            }
            else
            {
                logger.LogInformation("FeeItem {Id} already in ApprovalStatus '{Status}', skipping approve", outcome.TargetId, feeItem.ApprovalStatus);
            }
        }
        else
        {
            // Idempotent: only Reject when still Pending.
            // Reject() sets ApprovalStatus="Rejected" and clears ApprovalSubmittedAt; it keeps
            // RequiresApproval=true so the rejected fee stays out of the billable total.
            // (Do NOT ClearApprovalMarkers here — that would null the "Rejected" status.)
            if (feeItem.ApprovalStatus == "Pending")
            {
                feeItem.Reject(approver, outcome.Reason ?? "Rejected by approver");
                logger.LogInformation("FeeItem {Id} rejected via FeeAppointmentApproval", outcome.TargetId);
            }
            else
            {
                logger.LogInformation("FeeItem {Id} already in ApprovalStatus '{Status}', skipping reject", outcome.TargetId, feeItem.ApprovalStatus);
            }
        }

        // Recalculate fee totals after approval/rejection
        var fee = await dbContext.AppraisalFees
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.Id == feeItem.AppraisalFeeId, ct);
        fee?.RecalculateFromItems();
    }
}
