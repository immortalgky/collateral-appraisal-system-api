namespace Appraisal.Application.Features.Project.UpdateProjectTower;

/// <summary>Request to update an existing project tower.</summary>
public record UpdateProjectTowerRequest(
    string? TowerName = null,
    int? NumberOfUnits = null,
    int? NumberOfFloors = null,
    string? CondoRegistrationNumber = null,
    string? ConditionType = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    string? DocumentValidationType = null,
    bool? IsLocationCorrect = null,
    decimal? Distance = null,
    decimal? RoadWidth = null,
    decimal? RightOfWay = null,
    string? RoadSurfaceType = null,
    string? RoadSurfaceTypeOther = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    int? BuildingAge = null,
    string? BuildingFormType = null,
    string? ConstructionMaterialType = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? RoyalDecree = null,
    bool? IsForestBoundary = null,
    string? ForestBoundaryRemark = null,
    string? Remark = null
);
