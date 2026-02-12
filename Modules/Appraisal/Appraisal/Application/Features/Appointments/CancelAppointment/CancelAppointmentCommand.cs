using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appointments.CancelAppointment;

public record CancelAppointmentCommand(
    Guid AppraisalId,
    Guid AppointmentId,
    Guid ChangedBy,
    string? Reason = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
