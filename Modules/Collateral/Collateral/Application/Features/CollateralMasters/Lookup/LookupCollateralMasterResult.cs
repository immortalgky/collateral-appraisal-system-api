namespace Collateral.Application.Features.CollateralMasters.Lookup;

public record LookupCollateralMasterResult(
    Guid Id,
    string CollateralType,
    string? OwnerName,
    DateTime CreatedAt,
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
    // AppraisedValue removed (PR-4): values now live on master detail rows (LandDetail.AppraisalValue etc.)
    // and inside the engagement Snapshot JSON. Use the master-level LastAppraisedValue for display.
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName
);

public record LandDetailDto(
    string LandOfficeCode,
    string Province,
    string District,
    string SubDistrict,
    string TitleType,
    string TitleNumber,
    string? SurveyNumber,
    string? LandParcelNumber,
    string? Street,
    string? Village,
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
    // PR-5: LastConstructionInspectionId removed — CI list is in the engagement snapshot.
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate,
    // Three-value model (Phase C)
    decimal? UnitPrice,
    decimal? BuildingValue,
    decimal? AppraisalValue,
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
    string TitleType,
    string TitleNumber,
    string? SurveyNumber
);

public record CondoDetailDto(
    // LandOfficeCode is descriptive (nullable) — no longer a dedup-key field. TitleNumber/TitleType dropped.
    string? LandOfficeCode,
    string CondoRegistrationNumber,
    string BuildingNumber,
    string FloorNumber,
    string RoomNumber,
    string? CondoName,
    string? Province,
    string? District,
    string? SubDistrict,
    decimal? UsableArea,
    string? LocationType,
    int? BuildingAge,
    int? ConstructionYear,
    string? ModelName,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate,
    // Three-value model (Phase C)
    decimal? UnitPrice,
    decimal? BuildingValue,
    decimal? AppraisalValue
);

public record LeaseholdDetailDto(
    string LeaseRegistrationNo,
    Guid UnderlyingMasterId,
    string Lessor,
    string Lessee,
    DateOnly LeaseTermStart,
    DateOnly? LeaseTermEnd,
    int? LeaseTermMonths,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate
);

public record MachineDetailDto(
    string? MachineRegistrationNo,
    string? SerialNo,
    string? Brand,
    string? Model,
    string? Manufacturer,
    Guid? LastAppraisalId,
    string? LastAppraisalNumber,
    DateTime? LastAppraisedDate
);
