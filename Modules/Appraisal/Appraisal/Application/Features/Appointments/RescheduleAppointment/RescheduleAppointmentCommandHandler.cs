namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public class RescheduleAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
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

        appointment.Reschedule(command.ChangedBy, command.NewDateTime, command.Reason);

        return Unit.Value;
    }
}
