namespace Request.Contracts.Requests.Dtos;

public record SourceDto(
    string? RequestedBy,
    string? RequestedByName,
    string Channel
);