namespace Shared.Dtos;

public record ContactDto(
    string ContactPersonName,
    string ContactPersonContactNo,
    string? ProjectCode
);