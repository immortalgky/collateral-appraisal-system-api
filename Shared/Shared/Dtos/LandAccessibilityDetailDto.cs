namespace Shared.Dtos;

public record LandAccessibilityDetailDto(
    FrontageRoadDto FrontageRoad,
    string? RoadSurface,
    string? RoadSurfaceOther,
    string? PublicUtility,
    string? PublicUtilityOther,
    string? LandUse,
    string? LandUseOther,
    string? LandEntranceExit,
    string? LandEntranceExitOther,
    string? Transportation,
    string? TransportationOther
);
