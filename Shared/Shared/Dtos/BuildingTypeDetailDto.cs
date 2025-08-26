namespace Shared.Dtos;

public record BuildingTypeDetailDto(
    string BuildingType,
    string? BuildingTypeOther,
    short? TotalFloor
);
