namespace Request.Contracts.Requests.Dtos;

public record CondoInfoDto(
    string? CondoName,
    string? BuildingNo,
    string? RoomNo,
    string? FloorNo
);