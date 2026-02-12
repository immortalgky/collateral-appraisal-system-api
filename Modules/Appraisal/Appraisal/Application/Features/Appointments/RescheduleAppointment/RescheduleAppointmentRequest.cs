namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public record RescheduleAppointmentRequest(
    Guid ChangedBy,
    DateTime NewDateTime,
    string? Reason = null);
