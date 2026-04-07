namespace Appraisal.Application.Features.BlockVillage.GetVillageModels;

public record VillageModelAreaDetailResultDto(Guid Id, string? AreaDescription, decimal? AreaSize);

public record VillageModelSurfaceResultDto(
    Guid Id,
    int FromFloorNumber,
    int ToFloorNumber,
    string? FloorType,
    string? FloorStructureType,
    string? FloorStructureTypeOther,
    string? FloorSurfaceType,
    string? FloorSurfaceTypeOther
);

public record VillageModelDepreciationPeriodResultDto(
    Guid Id,
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation
);

public record VillageModelDepreciationDetailResultDto(
    Guid Id,
    string DepreciationMethod,
    string? AreaDescription,
    decimal Area,
    short Year,
    bool IsBuilding,
    decimal PricePerSqMBeforeDepreciation,
    decimal PriceBeforeDepreciation,
    decimal PricePerSqMAfterDepreciation,
    decimal PriceAfterDepreciation,
    decimal DepreciationYearPct,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation,
    List<VillageModelDepreciationPeriodResultDto> Periods
);

public record VillageModelDto(
    Guid Id,
    Guid AppraisalId,
    string? ModelName,
    string? ModelDescription,
    int? NumberOfHouse,
    decimal? StartingPrice,
    decimal? UsableAreaMin,
    decimal? UsableAreaMax,
    decimal? StandardUsableArea,
    decimal? LandAreaRai,
    decimal? LandAreaNgan,
    decimal? LandAreaWa,
    decimal? StandardLandArea,
    string? FireInsuranceCondition,
    List<Guid>? ImageDocumentIds,
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
    string? Remark,
    List<VillageModelAreaDetailResultDto> AreaDetails,
    List<VillageModelSurfaceResultDto> Surfaces,
    List<VillageModelDepreciationDetailResultDto> DepreciationDetails
);
