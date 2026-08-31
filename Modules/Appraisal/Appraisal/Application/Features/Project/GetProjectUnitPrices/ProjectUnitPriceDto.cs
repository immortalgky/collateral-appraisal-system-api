namespace Appraisal.Application.Features.Project.GetProjectUnitPrices;

/// <summary>
/// Unified unit-price DTO covering both Condo and LandAndBuilding fields.
/// Condo-only: Floor, TowerName, CondoRegistrationNumber, RoomNumber, IsPoolView, IsSouth, PriceIncrementPerFloor.
/// LB-only: PlotNumber, HouseNumber, NumberOfFloors, LandArea, IsNearGarden, LandIncreaseDecreaseAmount.
/// </summary>
public record ProjectUnitPriceDto(
    Guid? Id,
    Guid ProjectUnitId,
    int SequenceNumber,
    // Common unit fields
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Condo-only unit fields
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    // LB-only unit fields
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea,
    // Common flags
    bool IsCorner,
    bool IsEdge,
    bool IsOther,
    // Condo-only flags
    bool IsPoolView,
    bool IsSouth,
    // LB-only flags
    bool IsNearGarden,
    // Calculated values (common)
    decimal? AdjustPriceLocation,
    decimal? StandardPrice,
    decimal? TotalAppraisalValue,
    decimal? TotalAppraisalValueRounded,
    decimal? ForceSellingPrice,
    decimal? CoverageAmount,
    // Condo-only calculated
    decimal? PriceIncrementPerFloor,
    // LB-only calculated
    decimal? LandIncreaseDecreaseAmount
);
