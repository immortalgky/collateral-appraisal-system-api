namespace Appraisal.Application.Features.Appointments.CancelAppointment;

public record CancelAppointmentCommand(
    Guid AppraisalId,
    Guid AppointmentId,
    string ChangedBy,
    string? Reason = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;