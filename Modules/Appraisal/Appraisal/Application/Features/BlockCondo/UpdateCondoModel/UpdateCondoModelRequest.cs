namespace Appraisal.Application.Features.BlockCondo.UpdateCondoModel;

/// <summary>
/// Request to update a condo model
/// </summary>
public record UpdateCondoModelRequest(
    // Model Info
    string? ModelName = null,
    string? ModelDescription = null,
    string? BuildingNumber = null,
    // Pricing
    decimal? StartingPriceMin = null,
    decimal? StartingPriceMax = null,
    bool? HasMezzanine = null,
    // Usable Area
    decimal? UsableAreaMin = null,
    decimal? UsableAreaMax = null,
    decimal? StandardUsableArea = null,
    // Insurance
    string? FireInsuranceCondition = null,
    // Layout
    string? RoomLayoutType = null,
    string? RoomLayoutTypeOther = null,
    // Materials
    string? GroundFloorMaterialType = null,
    string? GroundFloorMaterialTypeOther = null,
    string? UpperFloorMaterialType = null,
    string? UpperFloorMaterialTypeOther = null,
    string? BathroomFloorMaterialType = null,
    string? BathroomFloorMaterialTypeOther = null,
    // Documents
    List<Guid>? ImageDocumentIds = null,
    // Area Details
    List<CondoModelAreaDetailDto>? AreaDetails = null,
    // Other
    string? Remark = null
);
