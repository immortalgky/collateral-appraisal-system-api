namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitPrices;

public record GetCondoUnitPricesResult(IReadOnlyList<CondoUnitPriceDto> UnitPrices);

public record CondoUnitPriceDto(
    Guid Id,
    Guid CondoUnitId,
    // Unit info (denormalized for display)
    int? Floor,
    string? TowerName,
    string? RoomNumber,
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Location flags
    bool IsCorner,
    bool IsEdge,
    bool IsPoolView,
    bool IsSouth,
    bool IsOther,
    // Calculated values
    decimal? AdjustPriceLocation,
    decimal? StandardPrice,
    decimal? PriceIncrementPerFloor,
    decimal? TotalAppraisalValue,
    decimal? TotalAppraisalValueRounded,
    decimal? ForceSellingPrice,
    decimal? CoverageAmount
);
