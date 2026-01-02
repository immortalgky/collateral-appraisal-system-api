namespace Request.Contracts.Requests.Dtos;

public record VesselDto(
    string? VesselType,
    string? VesselAppointmentLocation,
    string? HullIdentificationNumber,
    string? VesselRegistrationNumber
);