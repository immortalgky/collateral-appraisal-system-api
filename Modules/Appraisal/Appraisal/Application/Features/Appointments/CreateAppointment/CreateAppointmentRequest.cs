namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public record CreateAppointmentRequest(
    DateTime AppointmentDateTime,
    string AppointedBy,
    string? LocationDetail = null,
    string? ContactPerson = null,
    string? ContactPhone = null);