namespace Appraisal.Application.Features.Appointments.CancelReschedule;

public class CancelRescheduleAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<CancelRescheduleAppointmentCommand>
{
    public async Task<Unit> Handle(
        CancelRescheduleAppointmentCommand command,
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

        // Edit lock: once submitted for approval the approver owns the decision; discard is blocked.
        if (appointment.ApprovalSubmittedAt.HasValue)
            throw new InvalidOperationException(
                "Cannot cancel reschedule: an approval is currently awaiting review.");

        appointment.CancelReschedule(command.ChangedBy, command.Reason);

        return Unit.Value;
    }
}
