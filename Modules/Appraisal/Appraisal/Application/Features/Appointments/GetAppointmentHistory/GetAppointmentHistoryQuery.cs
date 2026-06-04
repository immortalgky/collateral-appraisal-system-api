namespace Appraisal.Application.Features.Appointments.GetAppointmentHistory;

public record GetAppointmentHistoryQuery(Guid AppraisalId) : IQuery<GetAppointmentHistoryResult>;
