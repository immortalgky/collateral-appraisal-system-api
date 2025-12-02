using System;

namespace Request.Contracts.Requests.Dtos;

public record AppointmentDto(
    DateTime? AppointmentDateTime,
    string? AppointmentLocation
);
