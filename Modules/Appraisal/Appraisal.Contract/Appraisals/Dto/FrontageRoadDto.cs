namespace Appraisal.Contracts.Appraisals.Dto;

public record FrontageRoadDto(
    decimal? RoadWidth,
    decimal? RightOfWay,
    decimal? WideFrontageOfLand,
    decimal? NoOfSideFacingRoad,
    decimal? RoadPassInFrontOfLand,
    string? LandAccessibility,
    string? LandAccessibilityDesc
);
