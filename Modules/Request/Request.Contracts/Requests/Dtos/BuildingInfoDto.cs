namespace Request.Contracts.Requests.Dtos;

public record BuildingInfoDto(
    string? BuildingType,
    decimal? UsableArea,
    int? NumberOfBuilding
);