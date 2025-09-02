namespace Appraisal.Contracts.Appraisals.Dto;

public record LandMiscellaneousDetailDto(
    decimal? PondArea,
    decimal? DepthPit,
    string? HasBuilding,
    string? HasBuildingOther
);
