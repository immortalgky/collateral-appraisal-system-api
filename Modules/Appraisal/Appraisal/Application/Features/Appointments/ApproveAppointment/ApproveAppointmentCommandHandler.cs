namespace Appraisal.Application.Features.Appointments.ApproveAppointment;

public class ApproveAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<ApproveAppointmentCommand>
{
    public async Task<Unit> Handle(
        ApproveAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var appointment = await dbContext.Appointments
                              .FirstOrDefaultAsync(a => a.Id == command.AppointmentId, cancellationToken)
                          ?? throw new NotFoundException("Appointment", command.AppointmentId);

        // Verify the appointment's assignment belongs to this appraisal
        var assignmentBelongs = appraisal.Assignments.Any(a => a.Id == appointment.AssignmentId);
        if (!assignmentBelongs)
            throw new InvalidOperationException("Appointment does not belong to this appraisal.");

        appointment.Approve(command.ApprovedBy);

        return Unit.Value;
    }
}
