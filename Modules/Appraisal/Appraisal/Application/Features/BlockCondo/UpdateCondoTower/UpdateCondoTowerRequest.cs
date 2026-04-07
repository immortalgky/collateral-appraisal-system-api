namespace Appraisal.Application.Features.BlockCondo.UpdateCondoTower;

public record UpdateCondoTowerRequest(
    // Tower Identification
    string? TowerName = null,
    int? NumberOfUnits = null,
    int? NumberOfFloors = null,
    string? CondoRegistrationNumber = null,
    List<Guid>? ModelTypeIds = null,
    // Condition & Obligation
    string? ConditionType = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    string? DocumentValidationType = null,
    // Location
    bool? IsLocationCorrect = null,
    decimal? Distance = null,
    decimal? RoadWidth = null,
    decimal? RightOfWay = null,
    string? RoadSurfaceType = null,
    string? RoadSurfaceTypeOther = null,
    // Decoration
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    // Building Info
    int? ConstructionYear = null,
    int? TotalNumberOfFloors = null,
    string? BuildingFormType = null,
    string? ConstructionMaterialType = null,
    // Materials
    string? GroundFloorMaterialType = null,
    string? GroundFloorMaterialTypeOther = null,
    string? UpperFloorMaterialType = null,
    string? UpperFloorMaterialTypeOther = null,
    string? BathroomFloorMaterialType = null,
    string? BathroomFloorMaterialTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    // Legal Restrictions
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? RoyalDecree = null,
    bool? IsForestBoundary = null,
    string? ForestBoundaryRemark = null,
    // Other
    string? Remark = null,
    List<Guid>? ImageDocumentIds = null
);
