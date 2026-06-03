using MassTransit;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Consumes FeeAppointmentApprovalResolvedIntegrationEvent from the Workflow module and applies
/// per-component decisions to the Appraisal domain:
/// - Appointment line: Approve → Appointment.Approve() | Reject → Appointment.RejectReschedule()
/// - Fee line:         Approve → AppraisalFeeItem.Approve() | Reject → AppraisalFeeItem.Reject()
///
/// InboxGuard prevents double-application on MassTransit retries.
/// Domain method calls are guarded against non-Pending status so retries are idempotent.
/// </summary>
public class FeeAppointmentApprovalResolvedIntegrationEventHandler(
    AppraisalDbContext dbContext,
    InboxGuard<AppraisalDbContext> inboxGuard,
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

        foreach (var outcome in msg.LineOutcomes)
        {
            if (outcome.LineType == "Appointment")
                await ApplyAppointmentOutcomeAsync(outcome, context.CancellationToken);
            else if (outcome.LineType == "Fee")
                await ApplyFeeOutcomeAsync(outcome, context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }

    private async Task ApplyAppointmentOutcomeAsync(FeeApprovalLineOutcome outcome, CancellationToken ct)
    {
        var appointment = await dbContext.Appointments
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == outcome.TargetId, ct);

        if (appointment is null)
        {
            logger.LogWarning("Appointment {Id} not found for fee approval outcome — skipping", outcome.TargetId);
            return;
        }

        if (outcome.Decision == "Approved")
        {
            // Idempotent: only call Approve when actually Pending
            if (appointment.Status == "Pending")
            {
                appointment.Approve(SystemActor);
                logger.LogInformation("Appointment {Id} approved via FeeAppointmentApproval", outcome.TargetId);
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
                appointment.RejectReschedule(SystemActor, outcome.Reason);
                logger.LogInformation("Appointment {Id} reschedule rejected via FeeAppointmentApproval", outcome.TargetId);
            }
            else
            {
                logger.LogInformation("Appointment {Id} already in status '{Status}', skipping reject-reschedule", outcome.TargetId, appointment.Status);
            }

            // Clear approval markers on rejection so a new draft can begin.
            // On approval, Approve() already clears them.
            appointment.ClearApprovalMarkers();
        }
    }

    private async Task ApplyFeeOutcomeAsync(FeeApprovalLineOutcome outcome, CancellationToken ct)
    {
        var feeItem = await dbContext.AppraisalFeeItems
            .FirstOrDefaultAsync(i => i.Id == outcome.TargetId, ct);

        if (feeItem is null)
        {
            logger.LogWarning("AppraisalFeeItem {Id} not found for fee approval outcome — skipping", outcome.TargetId);
            return;
        }

        if (outcome.Decision == "Approved")
        {
            // Idempotent: only Approve when still Pending
            if (feeItem.ApprovalStatus == "Pending")
            {
                feeItem.Approve(Guid.Empty);
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
                feeItem.Reject(Guid.Empty, outcome.Reason ?? "Rejected by approver");
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
