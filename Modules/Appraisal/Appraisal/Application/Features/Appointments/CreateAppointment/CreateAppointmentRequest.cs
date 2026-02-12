namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public record CreateAppointmentRequest(
    Guid AssignmentId,
    DateTime AppointmentDateTime,
    Guid AppointedBy,
    string? LocationDetail = null,
    string? ContactPerson = null,
    string? ContactPhone = null);
