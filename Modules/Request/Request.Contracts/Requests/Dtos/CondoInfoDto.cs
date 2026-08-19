namespace Request.Contracts.Requests.Dtos;

public record CondoInfoDto(
    string? CondoName,
    string? BuildingNo,
    string? CondoRegistrationNo,
    string? RoomNo,
    string? FloorNo,
    decimal? UsableArea
);