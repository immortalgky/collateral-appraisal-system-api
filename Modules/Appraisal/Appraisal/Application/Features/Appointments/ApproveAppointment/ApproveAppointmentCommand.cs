using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appointments.ApproveAppointment;

public record ApproveAppointmentCommand(
    Guid AppraisalId,
    Guid AppointmentId,
    string ApprovedBy
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;