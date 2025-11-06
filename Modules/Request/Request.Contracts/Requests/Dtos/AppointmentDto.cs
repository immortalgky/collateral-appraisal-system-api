namespace Request.Contracts.Requests.Dtos;

public record AppointmentDto(
    string AppointmentDateTime,
    string AppointmentLocation
);