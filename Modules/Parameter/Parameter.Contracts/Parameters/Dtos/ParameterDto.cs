namespace Parameter.Contracts.Parameters.Dtos;

public record ParameterDto
(
    long? ParId,
    string? Group,
    string? Country,
    string? Language,
    string? Code,
    string? Description,
    string? Active,
    string? SeqNo
);