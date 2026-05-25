namespace Collateral.Application.Features.CollateralMasters.Shared;

/// <summary>
/// Flat Dapper projection from collateral.vw_CollateralMasters.
/// Column aliases in the view use prefixes (Land_, Condo_, Lh_, Machine_) so Dapper
/// can map them to properties on this single flat class.
/// </summary>
public class CollateralMasterViewRow
{
    // Master identity
    public Guid Id { get; init; }
    public string CollateralType { get; init; } = null!;
    public string? OwnerName { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }

    // Engagement aggregates
    public int? EngagementCount { get; init; }
    public DateTime? LastAppraisedDate { get; init; }
    public decimal? LastAppraisedValue { get; init; }

    // Land columns
    public string? Land_LandOfficeCode { get; init; }
    public string? Land_Province { get; init; }
    public string? Land_District { get; init; }
    public string? Land_SubDistrict { get; init; }
    public string? Land_TitleType { get; init; }
    public string? Land_TitleNumber { get; init; }
    public string? Land_SurveyNumber { get; init; }
    public string? Land_LandParcelNumber { get; init; }
    public string? Land_Street { get; init; }
    public string? Land_Village { get; init; }
    public decimal? Land_Latitude { get; init; }
    public decimal? Land_Longitude { get; init; }
    public string? Land_LandShapeType { get; init; }
    public string? Land_LandZoneType { get; init; }
    public string? Land_UrbanPlanningType { get; init; }
    public decimal? Land_AccessRoadWidth { get; init; }
    public decimal? Land_RoadFrontage { get; init; }
    public decimal? Land_LandArea { get; init; }
    public bool? IsUnderConstructionAtLastAppraisal { get; init; }
    public decimal? OverallConstructionProgressPercent { get; init; }
    // PR-5: Land_LastConstructionInspectionId removed — CI list is in the engagement snapshot.
    public Guid? Land_LastAppraisalId { get; init; }
    public string? Land_LastAppraisalNumber { get; init; }
    public DateTime? Land_LastAppraisedDate { get; init; }
    // Three-value model
    public decimal? Land_UnitPrice { get; init; }
    public decimal? Land_BuildingCost { get; init; }
    public decimal? Land_AppraisalValue { get; init; }

    // Condo columns
    public string? Condo_LandOfficeCode { get; init; }
    public string? Condo_CondoRegistrationNumber { get; init; }
    public string? Condo_BuildingNumber { get; init; }
    public string? Condo_FloorNumber { get; init; }
    public string? Condo_RoomNumber { get; init; }
    public string? Condo_TitleNumber { get; init; }
    public string? Condo_TitleType { get; init; }
    public string? Condo_CondoName { get; init; }
    public string? Condo_Province { get; init; }
    public decimal? Condo_UsableArea { get; init; }
    public string? Condo_LocationType { get; init; }
    public int? Condo_BuildingAge { get; init; }
    public int? Condo_ConstructionYear { get; init; }
    public string? Condo_ModelName { get; init; }
    public Guid? Condo_LastAppraisalId { get; init; }
    public string? Condo_LastAppraisalNumber { get; init; }
    public DateTime? Condo_LastAppraisedDate { get; init; }
    // Three-value model
    public decimal? Condo_UnitPrice { get; init; }
    public decimal? Condo_BuildingCost { get; init; }
    public decimal? Condo_AppraisalValue { get; init; }

    // Leasehold columns
    public string? Lh_LeaseRegistrationNo { get; init; }
    public Guid? Lh_UnderlyingMasterId { get; init; }
    public string? Lh_Lessor { get; init; }
    public string? Lh_Lessee { get; init; }
    public DateTime? Lh_LeaseTermStart { get; init; }   // DateOnly stored as date → Dapper gives DateTime
    public DateTime? Lh_LeaseTermEnd { get; init; }
    public int? Lh_LeaseTermMonths { get; init; }
    public Guid? Lh_LastAppraisalId { get; init; }
    public string? Lh_LastAppraisalNumber { get; init; }
    public DateTime? Lh_LastAppraisedDate { get; init; }

    // Machine columns
    public string? Machine_MachineRegistrationNo { get; init; }
    public string? Machine_SerialNo { get; init; }
    public string? Machine_Brand { get; init; }
    public string? Machine_Model { get; init; }
    public string? Machine_Manufacturer { get; init; }
    public Guid? Machine_LastAppraisalId { get; init; }
    public string? Machine_LastAppraisalNumber { get; init; }
    public DateTime? Machine_LastAppraisedDate { get; init; }
}
