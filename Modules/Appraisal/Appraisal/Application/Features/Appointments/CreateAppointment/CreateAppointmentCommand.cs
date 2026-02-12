using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public record CreateAppointmentCommand(
    Guid AppraisalId,
    Guid AssignmentId,
    DateTime AppointmentDateTime,
    Guid AppointedBy,
    string? LocationDetail = null,
    string? ContactPerson = null,
    string? ContactPhone = null
) : ICommand<CreateAppointmentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
