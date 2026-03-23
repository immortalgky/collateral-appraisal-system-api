namespace Appraisal.Application.Features.Appointments.GetAppointment;

public record GetAppointmentQuery(Guid AppraisalId) : IQuery<GetAppointmentResult>;
