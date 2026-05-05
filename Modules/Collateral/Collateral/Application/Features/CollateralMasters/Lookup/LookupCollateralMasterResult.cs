namespace Collateral.Application.Features.CollateralMasters.Lookup;

public record LookupCollateralMasterResult(
    Guid Id,
    string CollateralType,
    string? OwnerName,
    DateTime CreatedOn,
    // Last engagement summary
    int EngagementCount,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue,
    // Type-specific detail (only the matching type is populated)
    LandDetailDto? LandDetail,
    CondoDetailDto? CondoDetail,
    LeaseholdDetailDto? LeaseholdDetail,
    MachineDetailDto? MachineDetail,
    // Prior company IDs for appeal exclusion
    IReadOnlyList<Guid> PriorAppraisalCompanyIds,
    // Most recent engagement — drives the FE's most-recent-only appeal exclusion store
    LastEngagementSummaryDto? LastEngagement
);

public record LastEngagementSummaryDto(
    Guid AppraisalId,
    string AppraisalNumber,
    string AppraisalType,
    DateTime AppraisalDate,
    decimal? AppraisedValue,
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName
);

public record LandDetailDto(
    string LandOfficeCode,
    string Province,
    string Amphur,
    string Tambon,
    string TitleDeedType,
    string TitleDeedNo,
    string? SurveyOrParcelNo,
    string? Street,
    string? Village,
    string? PostalCode,
    decimal? Latitude,
    decimal? Longitude,
    string? LandShapeType,
    string? LandZoneType,
    string? UrbanPlanningType,
    decimal? AccessRoadWidth,
    decimal? RoadFrontage,
    decimal? LandArea,
    bool IsUnderConstructionAtLastAppraisal,
    decimal? OverallConstructionProgressPercent,
    Guid? LastConstructionInspectionId,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue,
    decimal? LastTotalAppraisedValue,
    /// <summary>
    /// Alias titles belonging to the same multi-title group as this master.
    /// Empty for single-title properties (the common case).
    /// </summary>
    IReadOnlyList<AliasTitleDto> AliasTitles
);

/// <summary>
/// Dedup-key fields for an alias title in a multi-title Land group.
/// </summary>
public record AliasTitleDto(
    string TitleDeedType,
    string TitleDeedNo,
    string? SurveyOrParcelNo
);

public record CondoDetailDto(
    string LandOfficeCode,
    string CondoRegistrationNumber,
    string BuildingNumber,
    string FloorNumber,
    string UnitNumber,
    string TitleNumber,
    string TitleType,
    string? CondoName,
    string? Province,
    decimal? UsableArea,
    string? LocationType,
    int? BuildingAge,
    int? ConstructionYear,
    string? ModelName,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue
);

public record LeaseholdDetailDto(
    string LeaseRegistrationNo,
    Guid UnderlyingMasterId,
    string Lessor,
    string Lessee,
    DateOnly LeaseTermStart,
    DateOnly? LeaseTermEnd,
    int? LeaseTermMonths,
    decimal? AnnualRent,
    string? LeasePurpose,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue
);

public record MachineDetailDto(
    string? MachineRegistrationNo,
    string? SerialNo,
    string? Brand,
    string? Model,
    string? Manufacturer,
    string? EngineNo,
    string? ChassisNo,
    int? YearOfManufacture,
    string? MachineCondition,
    decimal? MachineAge,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue
);
