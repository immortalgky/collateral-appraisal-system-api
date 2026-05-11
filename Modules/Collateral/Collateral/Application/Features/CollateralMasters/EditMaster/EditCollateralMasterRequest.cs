namespace Collateral.Application.Features.CollateralMasters.EditMaster;

/// <summary>
/// HTTP request body for PATCH /collateral-masters/{id}.
/// All type-detail sub-objects are optional. Within each, all fields are nullable —
/// only non-null values are applied.
/// </summary>
public record EditCollateralMasterRequest(
    string? OwnerName,
    string Reason,
    LandEditFields? LandDetail,
    CondoEditFields? CondoDetail,
    LeaseholdEditFields? LeaseholdDetail,
    MachineEditFields? MachineDetail
);

public record LandEditFields(
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

public record CondoEditFields(
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

public record LeaseholdEditFields(
    string? LeaseRegistrationNo,
    string? Lessor,
    string? Lessee,
    DateOnly? LeaseTermStart,
    DateOnly? LeaseTermEnd,
    int? LeaseTermMonths
);

public record MachineEditFields(
    string? MachineRegistrationNo,
    string? SerialNo,
    string? Brand,
    string? Model,
    string? Manufacturer
);
