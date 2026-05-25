namespace Collateral.Application.Features.CollateralEngagements.SearchEngagements;

public record SearchCollateralEngagementsResult(PaginatedResult<CollateralEngagementSearchItemDto> Items);

/// <summary>
/// One row per engagement returned by the search endpoint.
///
/// IMPORTANT: Constructor parameter order MUST match the SELECT column order in
/// SearchCollateralEngagementsQueryHandler (SelectColumns constant) exactly.
/// Dapper materializes positional records by column ordinal, not by name.
/// If you add or reorder columns in the SELECT, mirror the change here.
/// </summary>
public record CollateralEngagementSearchItemDto(
    // 1 — Engagement identity
    Guid Id,
    Guid CollateralMasterId,
    Guid AppraisalId,
    string AppraisalNumber,
    Guid RequestId,
    string RequestNumber,
    string AppraisalType,
    DateTime AppraisalDate,
    string? AppraiserUserId,
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName,
    DateTime CreatedAt,
    // 13 — Engagement-time history (frozen at creation — historically accurate)
    string? AppraisedCollateralType,
    decimal? LandAreaInSqWa,
    decimal? AppraisalValue,       // Group-level appraisal value frozen at engagement time
    // 16 — Master metadata
    string CollateralType,
    string? OwnerName,
    // 18 — Aggregated building types for display (CSV, e.g. "01,02")
    string? BuildingTypeCodes,
    // 19 — Land identity for display
    string? Land_Province,
    string? Land_District,
    string? Land_SubDistrict,
    string? Land_TitleNumber,
    decimal? Land_Latitude,
    decimal? Land_Longitude,
    // 25 — Condo identity for display
    string? Condo_Province,
    string? Condo_TitleNumber,
    decimal? Condo_Latitude,
    decimal? Condo_Longitude,
    // 29 — Leasehold identity for display
    string? Lh_LeaseRegistrationNo
);
