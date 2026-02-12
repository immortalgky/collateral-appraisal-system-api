namespace Appraisal.Application.Features.Appointments.GetAppointments;

public record GetAppointmentsResult(List<AppointmentDto> Appointments);

public record AppointmentDto(
    Guid Id,
    Guid AssignmentId,
    Guid AppraisalId,
    DateTime AppointmentDateTime,
    DateTime? ProposedDate,
    string? LocationDetail,
    decimal? Latitude,
    decimal? Longitude,
    string Status,
    string? Reason,
    Guid? ApprovedBy,
    DateTime? ApprovedAt,
    int RescheduleCount,
    Guid AppointedBy,
    string? ContactPerson,
    string? ContactPhone,
    DateTime? CreatedOn);
