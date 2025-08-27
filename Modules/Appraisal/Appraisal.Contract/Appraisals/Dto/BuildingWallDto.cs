namespace Appraisal.Contracts.Appraisals.Dto;

public record BuildingWallDto(
    string? InteriorWall,
    string? InteriorWallOther,
    string? ExteriorWall,
    string? ExteriorWallOther
);
