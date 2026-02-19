namespace Appraisal.Application.Features.Appointments.CancelAppointment;

public record CancelAppointmentRequest(string ChangedBy, string? Reason = null);