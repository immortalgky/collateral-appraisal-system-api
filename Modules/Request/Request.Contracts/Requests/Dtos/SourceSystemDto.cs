namespace Request.Contracts.Requests.Dtos;

public record SourceSystemDto(
    string? Channel,
    DateTime RequestDate,
    string? RequestBy,
    string? RequestByName,
    DateTime CreatedDate,
    string? Creator,
    string? CreatorName);