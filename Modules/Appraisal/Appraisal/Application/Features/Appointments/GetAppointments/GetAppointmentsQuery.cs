namespace Appraisal.Application.Features.Appointments.GetAppointments;

public record GetAppointmentsQuery(Guid AppraisalId) : IQuery<GetAppointmentsResult>;
