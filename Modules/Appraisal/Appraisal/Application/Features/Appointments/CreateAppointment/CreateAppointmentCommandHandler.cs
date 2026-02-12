namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public class CreateAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<CreateAppointmentCommand, CreateAppointmentResult>
{
    public async Task<CreateAppointmentResult> Handle(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        _ = appraisal.Assignments.FirstOrDefault(a => a.Id == command.AssignmentId)
            ?? throw new NotFoundException("Assignment", command.AssignmentId);

        var appointment = Appointment.Create(
            command.AssignmentId,
            command.AppointmentDateTime,
            command.AppointedBy,
            command.LocationDetail,
            command.ContactPerson,
            command.ContactPhone);

        dbContext.Appointments.Add(appointment);

        return new CreateAppointmentResult(appointment.Id);
    }
}
