namespace Appraisal.Application.Features.BlockCondo.GetCondoTowers;

public record GetCondoTowersResult(IReadOnlyList<CondoTowerDto> Towers);

public record CondoTowerDto(
    Guid Id,
    Guid AppraisalId,
    // Tower Identification
    string? TowerName,
    int? NumberOfUnits,
    int? NumberOfFloors,
    string? CondoRegistrationNumber,
    List<Guid>? ModelTypeIds,
    // Condition & Obligation
    string? ConditionType,
    bool? HasObligation,
    string? ObligationDetails,
    string? DocumentValidationType,
    // Location
    bool? IsLocationCorrect,
    decimal? Distance,
    decimal? RoadWidth,
    decimal? RightOfWay,
    string? RoadSurfaceType,
    string? RoadSurfaceTypeOther,
    // Decoration
    string? DecorationType,
    string? DecorationTypeOther,
    // Building Info
    int? ConstructionYear,
    int? TotalNumberOfFloors,
    string? BuildingFormType,
    string? ConstructionMaterialType,
    // Materials
    string? GroundFloorMaterialType,
    string? GroundFloorMaterialTypeOther,
    string? UpperFloorMaterialType,
    string? UpperFloorMaterialTypeOther,
    string? BathroomFloorMaterialType,
    string? BathroomFloorMaterialTypeOther,
    List<string>? RoofType,
    string? RoofTypeOther,
    // Legal Restrictions
    bool? IsExpropriated,
    string? ExpropriationRemark,
    bool? IsInExpropriationLine,
    string? RoyalDecree,
    bool? IsForestBoundary,
    string? ForestBoundaryRemark,
    // Other
    string? Remark,
    List<Guid>? ImageDocumentIds
);
