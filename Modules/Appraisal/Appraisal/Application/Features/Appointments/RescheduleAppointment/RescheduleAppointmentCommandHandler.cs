using Appraisal.Application.Features.Shared;
using Appraisal.Application.Services;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public class RescheduleAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    ISender sender,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    AppraisalValuationSummaryService valuationSummaryService)
    : ICommandHandler<RescheduleAppointmentCommand>
{
    public async Task<Unit> Handle(
        RescheduleAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var appointment = await dbContext.Appointments
                              .Include(a => a.History)
                              .FirstOrDefaultAsync(a => a.Id == command.AppointmentId, cancellationToken)
                          ?? throw new NotFoundException("Appointment", command.AppointmentId);

        var assignmentBelongs = appraisal.Assignments.Any(a => a.Id == appointment.AssignmentId);
        if (!assignmentBelongs)
            throw new InvalidOperationException("Appointment does not belong to this appraisal.");

        // Edit lock: reject if an approval is currently awaiting (submitted but not resolved)
        if (appointment.ApprovalSubmittedAt.HasValue)
            throw new InvalidOperationException(
                "Cannot reschedule: an approval is currently awaiting review. Wait for the approval to be resolved before making further changes.");

        appointment.Reschedule(command.ChangedBy, command.NewDateTime, command.LocationDetail, command.Reason);

        // Follow the appraisal date to the new slot. Reschedule() has already moved the appointment's
        // own date, so this runs on BOTH branches below — the read side derives its fallback from the
        // appointment row without consulting approval state, and ValuationDate must not disagree with
        // it. Without this the appraisal date stays pinned to the ORIGINAL slot on every surface that
        // reads it (the printed book, both AS400 feeds, History Search, the +5-year reappraisal
        // anchor) until an unrelated pricing save happens to re-derive it. No-ops for an off-system
        // engagement, whose hand-keyed book date outranks any appointment.
        await valuationSummaryService.SyncValuationDateFromAppointmentAsync(
            command.AppraisalId, command.NewDateTime, cancellationToken);

        // Derive request source from the acting user — external companies use "Ext" approval rules;
        // internal bank users use "Int" rules (no company_id claim required).
        var requestSource = currentUser.ToFeeApprovalRequestSource();

        // Evaluate policy at edit time (read-only cross-module query)
        var verdict = await sender.Send(
            new EvaluateFeeAppointmentApprovalQuery(
                command.AppraisalId,
                RequestSource: requestSource,
                ProposedAppointmentDate: command.NewDateTime,
                RescheduleCount: appointment.RescheduleCount,
                CumulativeAddedFeeTotal: null),
            cancellationToken);

        if (!verdict.AppointmentRequiresApproval)
        {
            // Auto-apply: no approval needed
            appointment.Approve("system");

            // Notify Workflow module so it can (a) update WorkflowInstance.Variables["appointmentDate"]
            // and (b) recompute PendingTask.DueAt for appointment-anchored activities.
            outbox.Publish(new AppointmentDateChangedIntegrationEvent
            {
                AppraisalId = command.AppraisalId,
                CorrelationId = appraisal.RequestId,
                AssignmentId = appointment.AssignmentId,
                AppointmentDate = command.NewDateTime,
                OccurredOn = dateTimeProvider.ApplicationNow
            });
        }
        else
        {
            // Flag as requiring approval — user must call Submit for Approval
            appointment.FlagRequiresApproval();
        }

        return Unit.Value;
    }
}
