namespace Request.Contracts.Requests.Dtos;

public record ContactDto(
    string? ContactPersonName,
    string? ContactPersonContactNo,
    string? ProjectCode
);