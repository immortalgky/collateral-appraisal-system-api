using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public record RescheduleAppointmentCommand(
    Guid AppraisalId,
    Guid AppointmentId,
    Guid ChangedBy,
    DateTime NewDateTime,
    string? Reason = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
