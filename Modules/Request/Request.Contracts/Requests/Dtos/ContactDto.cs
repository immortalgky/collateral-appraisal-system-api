namespace Request.Contracts.Requests.Dtos;

public record ContactDto(
    string? ContactPersonName,
    string? ContactPersonPhone,
    string? DealerCode
);