using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public record RescheduleAppointmentCommand(
    Guid AppraisalId,
    Guid AppointmentId,
    string ChangedBy,
    DateTime NewDateTime,
    string LocationDetail,
    string? Reason = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;