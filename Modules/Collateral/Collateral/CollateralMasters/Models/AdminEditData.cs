namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Nullable admin-editable fields for a Land master.
/// Only non-null fields are applied; null means "leave unchanged".
/// Construction tracking and last-appraisal fields are system-managed and not included.
/// </summary>
public sealed record LandAdminEdit(
    string? LandOfficeCode,
    string? Province,
    string? District,
    string? SubDistrict,
    string? TitleType,
    string? TitleNumber,
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
    decimal? LandArea
);

/// <summary>
/// Nullable admin-editable fields for a Condo master.
/// </summary>
public sealed record CondoAdminEdit(
    string? LandOfficeCode,
    string? CondoRegistrationNumber,
    string? BuildingNumber,
    string? FloorNumber,
    string? RoomNumber,
    string? TitleNumber,
    string? TitleType,
    string? CondoName,
    string? Province,
    decimal? UsableArea,
    string? LocationType,
    int? BuildingAge,
    int? ConstructionYear,
    string? ModelName
);

/// <summary>
/// Nullable admin-editable fields for a Leasehold master.
/// </summary>
public sealed record LeaseholdAdminEdit(
    string? LeaseRegistrationNo,
    string? Lessor,
    string? Lessee,
    DateOnly? LeaseTermStart,
    DateOnly? LeaseTermEnd,
    int? LeaseTermMonths
);

/// <summary>
/// Nullable admin-editable fields for a Machine master.
/// </summary>
public sealed record MachineAdminEdit(
    string? MachineRegistrationNo,
    string? SerialNo,
    string? Brand,
    string? Model,
    string? Manufacturer
);
