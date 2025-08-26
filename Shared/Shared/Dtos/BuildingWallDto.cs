namespace Shared.Dtos;

public record BuildingWallDto(
    string? InteriorWall,
    string? InteriorWallOther,
    string? ExteriorWall,
    string? ExteriorWallOther
);
