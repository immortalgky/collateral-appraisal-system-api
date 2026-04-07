namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitPrices;

public record GetVillageUnitPricesResult(IReadOnlyList<VillageUnitPriceDto> UnitPrices);

public record VillageUnitPriceDto(
    Guid Id,
    Guid VillageUnitId,
    // Unit info (denormalized for display)
    int? SequenceNumber,
    string? PlotNumber,
    string? HouseNumber,
    string? ModelName,
    int? NumberOfFloors,
    decimal? LandArea,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Location flags
    bool IsCorner,
    bool IsEdge,
    bool IsNearGarden,
    bool IsOther,
    // Calculated values
    decimal? LandIncreaseDecreaseAmount,
    decimal? AdjustPriceLocation,
    decimal? StandardPrice,
    decimal? TotalAppraisalValue,
    decimal? TotalAppraisalValueRounded,
    decimal? ForceSellingPrice,
    decimal? CoverageAmount
);
