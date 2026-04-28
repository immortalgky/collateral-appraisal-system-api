namespace Appraisal.Application.Features.Project.GetProjectModels;

/// <summary>
/// DTO for a project model area detail (used in both create/update requests and query responses).
/// </summary>
public record ProjectModelAreaDetailDto(Guid? Id, string? AreaDescription, decimal? AreaSize);

/// <summary>DTO for a project model surface (LandAndBuilding only).</summary>
public record ProjectModelSurfaceDto(
    int FromFloorNumber,
    int ToFloorNumber,
    string? FloorType = null,
    string? FloorStructureType = null,
    string? FloorStructureTypeOther = null,
    string? FloorSurfaceType = null,
    string? FloorSurfaceTypeOther = null
);

/// <summary>DTO for a depreciation period within a depreciation detail.</summary>
public record ProjectModelDepreciationPeriodDto(
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation
);

/// <summary>DTO for a project model depreciation detail (LandAndBuilding only).</summary>
public record ProjectModelDepreciationDetailDto(
    string DepreciationMethod,
    string? AreaDescription = null,
    decimal Area = 0,
    short Year = 0,
    bool IsBuilding = true,
    decimal PricePerSqMBeforeDepreciation = 0,
    decimal PriceBeforeDepreciation = 0,
    decimal PricePerSqMAfterDepreciation = 0,
    decimal PriceAfterDepreciation = 0,
    decimal DepreciationYearPct = 0,
    decimal TotalDepreciationPct = 0,
    decimal PriceDepreciation = 0,
    List<ProjectModelDepreciationPeriodDto>? Periods = null
);

/// <summary>
/// Full project model DTO for query responses.
/// Contains the superset of Condo + LandAndBuilding model fields.
/// Type-specific fields will be null when not applicable.
/// </summary>
public record ProjectModelDto(
    Guid Id,
    Guid ProjectId,
    // Common
    string? ModelName,
    string? ModelDescription,
    string? BuildingNumber,            // Condo
    int? NumberOfHouse,               // LB
    decimal? StartingPrice,           // LB
    decimal? StartingPriceMin,        // Condo
    decimal? StartingPriceMax,        // Condo
    decimal? StandardPrice,
    bool? HasMezzanine,
    decimal? UsableAreaMin,
    decimal? UsableAreaMax,
    decimal? StandardUsableArea,
    string? FireInsuranceCondition,
    string? RoomLayoutType,           // Condo
    string? RoomLayoutTypeOther,      // Condo
    string? GroundFloorMaterialType,
    string? GroundFloorMaterialTypeOther,
    string? UpperFloorMaterialType,
    string? UpperFloorMaterialTypeOther,
    string? BathroomFloorMaterialType,
    string? BathroomFloorMaterialTypeOther,
    List<Guid>? ImageDocumentIds,
    string? Remark,
    // LB-specific
    decimal? LandAreaRai,
    decimal? LandAreaNgan,
    decimal? LandAreaWa,
    decimal? StandardLandArea,
    string? BuildingType,
    string? BuildingTypeOther,
    decimal? NumberOfFloors,
    string? DecorationType,
    string? DecorationTypeOther,
    bool? IsEncroachingOthers,
    string? EncroachingOthersRemark,
    decimal? EncroachingOthersArea,
    string? BuildingMaterialType,
    string? BuildingStyleType,
    bool? IsResidential,
    int? BuildingAge,
    int? ConstructionYear,
    string? ResidentialRemark,
    string? ConstructionStyleType,
    string? ConstructionStyleRemark,
    List<string>? StructureType,
    string? StructureTypeOther,
    List<string>? RoofFrameType,
    string? RoofFrameTypeOther,
    List<string>? RoofType,
    string? RoofTypeOther,
    List<string>? CeilingType,
    string? CeilingTypeOther,
    List<string>? InteriorWallType,
    string? InteriorWallTypeOther,
    List<string>? ExteriorWallType,
    string? ExteriorWallTypeOther,
    List<string>? FenceType,
    string? FenceTypeOther,
    string? ConstructionType,
    string? ConstructionTypeOther,
    string? UtilizationType,
    string? UtilizationTypeOther,
    // Owned collections
    List<ProjectModelAreaDetailDto> AreaDetails,
    List<ProjectModelSurfaceDto> Surfaces,
    List<ProjectModelDepreciationDetailDto> DepreciationDetails
);
