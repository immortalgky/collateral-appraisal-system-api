using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public class RescheduleAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    ISender sender)
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

        appointment.Reschedule(command.ChangedBy, command.NewDateTime, command.Reason);

        // Evaluate policy at edit time (read-only cross-module query)
        var verdict = await sender.Send(
            new EvaluateFeeAppointmentApprovalQuery(
                command.AppraisalId,
                RequestSource: "Ext",
                ProposedAppointmentDate: command.NewDateTime,
                RescheduleCount: appointment.RescheduleCount,
                CumulativeAddedFeeTotal: null),
            cancellationToken);

        if (!verdict.AppointmentRequiresApproval)
        {
            // Auto-apply: no approval needed
            appointment.Approve("system");
        }
        else
        {
            // Flag as requiring approval — user must call Submit for Approval
            appointment.FlagRequiresApproval();
        }

        return Unit.Value;
    }
}
