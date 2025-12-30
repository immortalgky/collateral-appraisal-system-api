using System;

namespace Request.Contracts.Requests.Dtos;

public record AppointmentDto(
    DateTime? AppointmentDate,
    string? AppointmentLocation
);
