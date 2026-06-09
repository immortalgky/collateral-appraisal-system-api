using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appointments.CancelReschedule;

public record CancelRescheduleAppointmentCommand(
    Guid AppraisalId,
    Guid AppointmentId,
    string ChangedBy,
    string? Reason = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
