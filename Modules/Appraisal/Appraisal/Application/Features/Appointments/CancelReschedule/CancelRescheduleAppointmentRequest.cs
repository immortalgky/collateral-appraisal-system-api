namespace Appraisal.Application.Features.Appointments.CancelReschedule;

public record CancelRescheduleAppointmentRequest(string ChangedBy, string? Reason = null);
