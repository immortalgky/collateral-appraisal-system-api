namespace Appraisal.Contracts.Appraisals.Dto;

public record CondominiumLocationDto(
    bool? CondoLocation,
    string? Street,
    string? Soi,
    decimal? Distance,
    decimal? RoadWidth,
    decimal? RightOfWay,
    string? RoadSurface,
    string? PublicUtility,
    string? PublicUtilityOther
);
