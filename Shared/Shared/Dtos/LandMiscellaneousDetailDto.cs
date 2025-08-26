namespace Shared.Dtos;

public record LandMiscellaneousDetailDto(
    decimal? PondArea,
    decimal? DepthPit,
    string? HasBuilding,
    string? HasBuildingOther
);
