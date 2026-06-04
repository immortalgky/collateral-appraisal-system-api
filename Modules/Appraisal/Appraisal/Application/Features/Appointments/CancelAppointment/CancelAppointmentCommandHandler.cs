namespace Appraisal.Application.Features.Appointments.CancelAppointment;

public class CancelAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<CancelAppointmentCommand>
{
    public async Task<Unit> Handle(
        CancelAppointmentCommand command,
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

        // Edit lock: reject if an approval is currently awaiting (submitted but not yet resolved) —
        // mirrors the reschedule guard so a pending reschedule can't be cancelled out from under the approver.
        if (appointment.ApprovalSubmittedAt.HasValue)
            throw new InvalidOperationException(
                "Cannot cancel: an approval is currently awaiting review. Wait for the approval to be resolved.");

        appointment.Cancel(command.ChangedBy, command.Reason);

        return Unit.Value;
    }
}
