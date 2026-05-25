namespace Collateral.Application.Features.CollateralEngagements.SearchEngagements;

/// <summary>
/// Engagement-grain search — one result row per past appraisal event, not per master.
/// Filters use engagement-time values (AppraisedCollateralType, LandAreaInSqWa) so
/// searches are historically accurate even if the master has since been re-classified.
/// </summary>
public record SearchCollateralEngagementsQuery(
    PaginationRequest PaginationRequest,
    // Appraisal-side filters
    string? AppraisalReportNo,
    DateOnly? AppraisalDateFrom,
    DateOnly? AppraisalDateTo,
    // Collateral-side filters
    string? TitleDeedNo,              // LIKE against Land_TitleNumber, Condo_TitleNumber, Lh_LeaseRegistrationNo
    string[]? CollateralTypes,        // filter on AppraisedCollateralType IN (...) — values are L/LB/U/LSL/LSB/LS/MAC
    string[]? BuildingTypeCodes,      // EXISTS against CollateralEngagementBuildings
    decimal? LandAreaFromSqWa,        // filter on LandAreaInSqWa
    decimal? LandAreaToSqWa,
    string? CustomerName,             // LIKE on OwnerName
    // Geo filter
    decimal? CenterLat,
    decimal? CenterLng,
    decimal? RadiusKm,
    // Address filters
    string? SubDistrict,
    string? District,
    string? Province,
    // Drill-down filter — pin "click → list its engagements"
    /// <summary>
    /// When set, restricts results to engagements for a single CollateralMaster.
    /// Powers the "click a collateral pin → list all its past appraisals" flow.
    /// </summary>
    Guid? CollateralMasterId,
    // Sort
    string? Sort
) : IQuery<SearchCollateralEngagementsResult>;
