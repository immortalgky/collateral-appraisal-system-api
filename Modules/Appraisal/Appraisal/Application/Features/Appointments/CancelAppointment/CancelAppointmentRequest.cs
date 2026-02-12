namespace Appraisal.Application.Features.Appointments.CancelAppointment;

public record CancelAppointmentRequest(Guid ChangedBy, string? Reason = null);
