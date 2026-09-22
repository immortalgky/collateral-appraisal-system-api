namespace Request.Contracts.Requests.Dtos;

public record VehicleDto(
    string? VehicleType,
    string? VehicleRegistrationNumber,
    string? VehicleAppointmentLocation,
    string? VIN,
    string? LicensePlateNumber
);
