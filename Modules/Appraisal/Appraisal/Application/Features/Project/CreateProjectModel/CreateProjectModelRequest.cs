using Appraisal.Application.Features.Project.GetProjectModels;

namespace Appraisal.Application.Features.Project.CreateProjectModel;

/// <summary>Request to create a project model (Condo or LandAndBuilding).</summary>
public record CreateProjectModelRequest(
    // Common
    string? ModelName = null,
    string? ModelDescription = null,
    string? BuildingNumber = null,        // Condo
    int? NumberOfHouse = null,            // LB
    decimal? StartingPrice = null,        // LB
    decimal? StartingPriceMin = null,     // Condo
    decimal? StartingPriceMax = null,     // Condo
    decimal? StandardPrice = null,
    bool? HasMezzanine = null,
    decimal? UsableAreaMin = null,
    decimal? UsableAreaMax = null,
    decimal? StandardUsableArea = null,
    string? FireInsuranceCondition = null,
    string? RoomLayoutType = null,        // Condo
    string? RoomLayoutTypeOther = null,   // Condo
    string? GroundFloorMaterialType = null,
    string? GroundFloorMaterialTypeOther = null,
    string? UpperFloorMaterialType = null,
    string? UpperFloorMaterialTypeOther = null,
    string? BathroomFloorMaterialType = null,
    string? BathroomFloorMaterialTypeOther = null,
    List<Guid>? ImageDocumentIds = null,
    string? Remark = null,
    // LB-specific
    decimal? LandAreaRai = null,
    decimal? LandAreaNgan = null,
    decimal? LandAreaWa = null,
    decimal? StandardLandArea = null,
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    decimal? NumberOfFloors = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    bool? IsEncroachingOthers = null,
    string? EncroachingOthersRemark = null,
    decimal? EncroachingOthersArea = null,
    string? BuildingMaterialType = null,
    string? BuildingStyleType = null,
    bool? IsResidential = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    string? ResidentialRemark = null,
    string? ConstructionStyleType = null,
    string? ConstructionStyleRemark = null,
    List<string>? StructureType = null,
    string? StructureTypeOther = null,
    List<string>? RoofFrameType = null,
    string? RoofFrameTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    List<string>? CeilingType = null,
    string? CeilingTypeOther = null,
    List<string>? InteriorWallType = null,
    string? InteriorWallTypeOther = null,
    List<string>? ExteriorWallType = null,
    string? ExteriorWallTypeOther = null,
    List<string>? FenceType = null,
    string? FenceTypeOther = null,
    string? ConstructionType = null,
    string? ConstructionTypeOther = null,
    string? UtilizationType = null,
    string? UtilizationTypeOther = null,
    // Collections
    List<ProjectModelAreaDetailDto>? AreaDetails = null,
    List<ProjectModelSurfaceDto>? Surfaces = null,
    List<ProjectModelDepreciationDetailDto>? DepreciationDetails = null
);
