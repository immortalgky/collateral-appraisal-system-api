namespace Appraisal.Application.Features.Project.GetProjectTowers;

/// <summary>DTO for a project tower image.</summary>
public record ProjectTowerImageDto(
    Guid Id,
    Guid GalleryPhotoId,
    int DisplaySequence,
    string? Title,
    string? Description,
    bool IsThumbnail
);

/// <summary>DTO representing a project tower (Condo only).</summary>
public record ProjectTowerDto(
    Guid Id,
    Guid ProjectId,
    // Tower Identification
    string? TowerName,
    int? NumberOfUnits,
    int? NumberOfFloors,
    string? CondoRegistrationNumber,
    // Condition & Obligation
    string? ConditionType,
    string? HasObligation,
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
    int? BuildingAge,
    string? BuildingFormType,
    string? ConstructionMaterialType,
    // Materials
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
    List<ProjectTowerImageDto> Images
);
