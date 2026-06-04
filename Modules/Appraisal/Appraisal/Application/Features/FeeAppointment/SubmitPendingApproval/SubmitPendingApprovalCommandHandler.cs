using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.FeeAppointment.SubmitPendingApproval;

/// <summary>
/// Handles submitting draft pending items (appointment + fee items) for bank approval.
///
/// Flow:
/// 1. Load appraisal + verify the named assignment belongs to the requesting company (IDOR gate).
/// 2. Gather all draft items: appointment with RequiresApproval=true and ApprovalSubmittedAt=null;
///    fee items with RequiresApproval=true, ApprovalStatus="Pending", and ApprovalSubmittedAt=null.
/// 3. Stamp each draft item's ApprovalSubmittedAt = now.
/// 4. Build the FeeApprovalRequestedLineDto list and publish the existing
///    FeeAppointmentApprovalRequestedIntegrationEvent via the outbox.
/// </summary>
public class SubmitPendingApprovalCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    IIntegrationEventOutbox outbox,
    ILogger<SubmitPendingApprovalCommandHandler> logger
) : ICommandHandler<SubmitPendingApprovalCommand, Unit>
{
    public async Task<Unit> Handle(
        SubmitPendingApprovalCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        // ─── IDOR gate: verify the requesting company owns the named assignment ───
        var assignment = appraisal.Assignments
            .FirstOrDefault(a => a.Id == command.AssignmentId);

        if (assignment is null)
            throw new InvalidOperationException(
                $"Assignment {command.AssignmentId} not found on appraisal {command.AppraisalId}");

        if (!string.Equals(assignment.AssigneeCompanyId, command.RequestedByCompanyId,
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Company {RequestedByCompanyId} attempted to submit fee/appointment approval for assignment {AssignmentId} " +
                "owned by company {AssigneeCompanyId}. Request rejected.",
                command.RequestedByCompanyId, command.AssignmentId, assignment.AssigneeCompanyId);
            throw new UnauthorizedAccessException(
                "Your company does not own this assignment");
        }

        var requiresApprovalLines = new List<FeeApprovalRequestedLineDto>();

        // ─── Appointment draft ──────────────────────────────────────────────────
        var appointment = await dbContext.Appointments
            .Include(a => a.History)
            .Where(a => a.AssignmentId == command.AssignmentId
                        && a.RequiresApproval
                        && a.ApprovalSubmittedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is not null)
        {
            appointment.MarkApprovalSubmitted();

            // The date before this reschedule — the most recent 'Rescheduled' history entry's
            // prior value. Snapshotted so the approver can see old → new.
            var previousDate = appointment.History
                .Where(h => h.ChangeType == "Rescheduled")
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => (DateTime?)h.PreviousAppointmentDateTime)
                .FirstOrDefault();

            requiresApprovalLines.Add(new FeeApprovalRequestedLineDto(
                "Appointment",
                appointment.Id,
                appointment.AppointmentDateTime,
                appointment.RescheduleCount,
                null, null, null,
                previousDate));

            logger.LogInformation(
                "Stamping appointment {AppointmentId} as submitted for appraisal {AppraisalId}",
                appointment.Id, command.AppraisalId);
        }

        // ─── Fee item drafts ────────────────────────────────────────────────────
        var fee = await dbContext.AppraisalFees
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.AssignmentId == command.AssignmentId, cancellationToken);

        if (fee is not null)
        {
            var draftItems = fee.Items
                .Where(i => i.Source == FeeItemSource.User
                            && i.RequiresApproval
                            && i.ApprovalStatus == "Pending"
                            && i.ApprovalSubmittedAt == null)
                .ToList();

            foreach (var item in draftItems)
            {
                item.MarkApprovalSubmitted();

                requiresApprovalLines.Add(new FeeApprovalRequestedLineDto(
                    "Fee",
                    item.Id,
                    null, null,
                    item.FeeCode,
                    item.FeeDescription,
                    item.FeeAmount));
            }

            if (draftItems.Count > 0)
                logger.LogInformation(
                    "Stamping {Count} fee item(s) as submitted for assignment {AssignmentId}",
                    draftItems.Count, command.AssignmentId);
        }

        if (requiresApprovalLines.Count == 0)
            throw new InvalidOperationException(
                "No pending draft items found to submit. Either there are no changes requiring approval, " +
                "or all changes have already been submitted.");

        // ─── Publish cross-module approval request ─────────────────────────────
        outbox.Publish(
            new FeeAppointmentApprovalRequestedIntegrationEvent
            {
                AppraisalId = command.AppraisalId,
                RequestSource = "Ext",
                Lines = requiresApprovalLines
            },
            correlationId: command.AppraisalId.ToString());

        logger.LogInformation(
            "Published FeeAppointmentApprovalRequested for appraisal {AppraisalId}, {Count} lines",
            command.AppraisalId, requiresApprovalLines.Count);

        return Unit.Value;
    }
}
