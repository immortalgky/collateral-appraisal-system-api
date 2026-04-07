namespace Appraisal.Application.Features.BlockVillage.CreateVillageModel;

public record VillageModelAreaDetailDto(string? AreaDescription, decimal? AreaSize);

public record VillageModelSurfaceDto(
    int FromFloorNumber,
    int ToFloorNumber,
    string? FloorType = null,
    string? FloorStructureType = null,
    string? FloorStructureTypeOther = null,
    string? FloorSurfaceType = null,
    string? FloorSurfaceTypeOther = null
);

public record VillageModelDepreciationPeriodDto(
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation
);

public record VillageModelDepreciationDetailDto(
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
    List<VillageModelDepreciationPeriodDto>? Periods = null
);
