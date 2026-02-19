namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public record RescheduleAppointmentRequest(
    string ChangedBy,
    DateTime NewDateTime,
    string? Reason = null);