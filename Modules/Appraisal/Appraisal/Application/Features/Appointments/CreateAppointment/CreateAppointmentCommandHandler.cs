using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public class CreateAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider)
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
                         throw new BadRequestException("No active assignment found for this appraisal.");

        var hasActiveAppointment = await dbContext.Appointments
            .AnyAsync(a => a.AssignmentId == assignment.Id
                           && (a.Status == "Pending" || a.Status == "Appointed"),
                cancellationToken);

        if (hasActiveAppointment)
            throw new BadRequestException(
                "This assignment already has an active appointment. Cancel it before creating a new one.");

        var appointment = Appointment.Create(
            assignment.Id,
            command.AppointmentDateTime,
            command.AppointedBy,
            command.LocationDetail,
            command.ContactPerson,
            command.ContactPhone);

        dbContext.Appointments.Add(appointment);

        // Notify Workflow module so it can (a) store appointmentDate in WorkflowInstance.Variables
        // (read by TaskActivity on next task assignment as the SLA anchor) and (b) recompute
        // PendingTask.DueAt for any appointment-anchored task already active on this correlation.
        // Appointment.Create sets Status = "Appointed" immediately — the date is confirmed.
        outbox.Publish(new AppointmentDateChangedIntegrationEvent
        {
            AppraisalId = command.AppraisalId,
            CorrelationId = appraisal.RequestId,
            AssignmentId = assignment.Id,
            AppointmentDate = command.AppointmentDateTime,
            OccurredOn = dateTimeProvider.ApplicationNow
        });

        return new CreateAppointmentResult(appointment.Id);
    }
}
