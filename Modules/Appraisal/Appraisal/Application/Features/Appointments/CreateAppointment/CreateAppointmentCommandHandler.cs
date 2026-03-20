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

        var assignment = appraisal.Assignments
                             .FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Assigned
                                                  || a.AssignmentStatus == AssignmentStatus.InProgress
                                                  || a.AssignmentStatus == AssignmentStatus.Pending)
                         ??
                         throw new InvalidOperationException("No active assignment found for this appraisal.");

        var hasActiveAppointment = await dbContext.Appointments
            .AnyAsync(a => a.AssignmentId == assignment.Id
                           && (a.Status == "Pending" || a.Status == "Approved"),
                cancellationToken);

        if (hasActiveAppointment)
            throw new InvalidOperationException(
                "This assignment already has an active appointment. Complete or cancel it before creating a new one.");

        var appointment = Appointment.Create(
            assignment.Id,
            command.AppointmentDateTime,
            command.AppointedBy,
            command.LocationDetail,
            command.ContactPerson,
            command.ContactPhone);

        dbContext.Appointments.Add(appointment);

        return new CreateAppointmentResult(appointment.Id);
    }
}