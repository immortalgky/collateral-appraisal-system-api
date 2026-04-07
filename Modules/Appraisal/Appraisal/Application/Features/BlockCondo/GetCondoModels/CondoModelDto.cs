namespace Appraisal.Application.Features.BlockCondo.GetCondoModels;

/// <summary>
/// DTO representing a condo model with all its fields
/// </summary>
public record CondoModelDto(
    Guid Id,
    Guid AppraisalId,
    // Model Info
    string? ModelName,
    string? ModelDescription,
    string? BuildingNumber,
    // Pricing
    decimal? StartingPriceMin,
    decimal? StartingPriceMax,
    bool? HasMezzanine,
    // Usable Area
    decimal? UsableAreaMin,
    decimal? UsableAreaMax,
    decimal? StandardUsableArea,
    // Insurance
    string? FireInsuranceCondition,
    // Layout
    string? RoomLayoutType,
    string? RoomLayoutTypeOther,
    // Materials
    string? GroundFloorMaterialType,
    string? GroundFloorMaterialTypeOther,
    string? UpperFloorMaterialType,
    string? UpperFloorMaterialTypeOther,
    string? BathroomFloorMaterialType,
    string? BathroomFloorMaterialTypeOther,
    // Documents
    List<Guid>? ImageDocumentIds,
    // Area Details
    List<CondoModelAreaDetailDto> AreaDetails,
    // Other
    string? Remark
);
