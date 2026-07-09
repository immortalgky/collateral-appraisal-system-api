namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public record RescheduleAppointmentRequest(
    string ChangedBy,
    DateTime NewDateTime,
    string LocationDetail,
    string? Reason = null);