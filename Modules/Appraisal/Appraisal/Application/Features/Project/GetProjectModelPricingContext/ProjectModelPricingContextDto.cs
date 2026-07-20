namespace Appraisal.Application.Features.Project.GetProjectModelPricingContext;

/// <summary>
/// Project-level pricing fields (always present).
/// </summary>
public record ProjectContextDto(
    decimal? Latitude,
    decimal? Longitude,
    string? Province,
    string? District,
    string? SubDistrict,
    string? Road,
    string? Developer,
    string? ProjectName,
    string? LandOffice,
    /// <summary>Project-total land area in Wa (distinct from per-model plot land).</summary>
    decimal? ProjectLandAreaSquareWa
);

/// <summary>
/// Building-level pricing fields for a Condo tower.
/// Null for LandAndBuilding projects (building-level fields resolve from the Model instead).
/// </summary>
public record TowerContextDto(
    int? BuildingAge,
    decimal? NumberOfFloors,
    string? DecorationType,
    List<string>? RoofType,
    string? StructureType,
    decimal? RoadWidth,
    decimal? Distance,
    decimal? RightOfWay,
    string? RoadSurfaceType
);

/// <summary>
/// Model-level pricing fields (always present).
/// Building-level fields (ConstructionYear, NumberOfFloors, etc.) are populated for
/// LandAndBuilding models (where they live on the Model entity).
/// For Condo models these building fields come from the Tower and will be null here.
/// </summary>
public record ModelContextDto(
    string? ModelName,
    decimal? UsableAreaMin,
    decimal? UsableAreaMax,
    decimal? StandardUsableArea,
    bool? HasMezzanine,
    string? RoomLayoutType,
    string? FireInsuranceCondition,
    string? GroundFloorMaterialType,
    string? UpperFloorMaterialType,
    string? BathroomFloorMaterialType,
    // LandAndBuilding-specific scalar fields
    int? BuildingAge,
    string? UtilizationType,
    decimal? StartingPriceMin,
    decimal? StartingPriceMax,
    /// <summary>
    /// Representative per-model plot land area in sq.wa for pricing
    /// (LandAndBuilding only). Sourced from <c>ProjectModel.StandardLandArea</c>
    /// — i.e. the model's canonical "typical plot" — NOT a literal min/max value.
    /// The DTO field name is preserved so existing FE factor seeds keyed on
    /// <c>landAreaSquareWa</c> continue to resolve.
    /// </summary>
    decimal? LandAreaSquareWa,
    // Building-level fields — populated for LandAndBuilding; null for Condo (use Tower instead)
    decimal? NumberOfFloors,
    string? DecorationType,
    List<string>? RoofType,
    List<string>? StructureType,
    decimal? RoadWidth,
    decimal? Distance,
    decimal? RightOfWay,
    string? RoadSurfaceType
);

/// <summary>
/// Combined pricing context for a project model.
/// Tower is null for LandAndBuilding projects.
/// </summary>
public record ProjectModelPricingContextDto(
    ProjectContextDto Project,
    TowerContextDto? Tower,
    ModelContextDto Model
);
