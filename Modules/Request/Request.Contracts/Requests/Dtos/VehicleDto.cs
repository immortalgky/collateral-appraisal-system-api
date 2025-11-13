namespace Request.Contracts.Requests.Dtos;

public record VehicleDto(
    string? VehicleType,
    string? VehicleAppointmentLocation,
    string? ChassisNumber
);
