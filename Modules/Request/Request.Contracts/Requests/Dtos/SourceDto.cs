namespace Request.Contracts.Requests.Dtos;

public record SourceDto(
    string Channel,
    long RequestedBy
);