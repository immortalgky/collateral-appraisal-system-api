namespace Collateral.Application.Features.HistorySearch;

/// <summary>
/// Date range period shortcuts for history search.
/// If DateFrom/DateTo are provided explicitly they override the Period.
/// </summary>
public enum Period
{
    Past3y,
    Past2y,
    Past1y,
    Current
}

/// <summary>
/// Query that drives the History Search (pin) feature.
/// Returns CollateralMaster pins and MarketComparable pins. When a centre point
/// (CenterLat/CenterLon) is supplied, results are filtered to RadiusKm and ordered
/// by distance. When the centre is omitted (FSD §2.6.7 — geo fields optional), the
/// radius filter and distance ordering are skipped and results are ordered by date.
/// </summary>
public record HistorySearchQuery(
    /// <summary>Centre latitude. Optional — when null, no radius filter / distance sort.</summary>
    decimal? CenterLat,
    /// <summary>Centre longitude. Optional — when null, no radius filter / distance sort.</summary>
    decimal? CenterLon,
    /// <summary>Radius in km — capped server-side at 50 km. Only applied when a centre is given.</summary>
    decimal? RadiusKm,
    Period Period,
    // Green-only filters (apply only to CollateralMaster pins; MC pins ignore these).
    /// <summary>FSD: หมายเลขเล่มประเมิน — exact / LIKE on type-specific LastAppraisalNumber.</summary>
    string? AppraisalReportNo,
    /// <summary>FSD: เลขที่โฉนด — LIKE on type-specific TitleNumber.</summary>
    string? TitleDeedNo,
    /// <summary>
    /// FSD: ประเภทหลักประกัน — match on CollateralType code(s). Multi-value because the FSD's single
    /// "Leasehold" option maps to three stored codes (LSL/LSB/LS); the FE expands it before sending.
    /// Codes: L, LB, U, LSL, LSB, LS, MAC.
    /// </summary>
    string[]? CollateralTypes,
    /// <summary>FSD: ชื่อลูกค้า — LIKE on CollateralMaster.OwnerName.</summary>
    string? CustomerName,
    /// <summary>FSD: เนื้อที่ดิน Sq.Wa — Land_LandArea range. Land-type collaterals only.</summary>
    decimal? LandAreaFromSqWa,
    decimal? LandAreaToSqWa,
    // Both-pin filters.
    decimal? ValueFrom,
    decimal? ValueTo,
    /// <summary>Explicit date-from — overrides Period when set.</summary>
    DateOnly? DateFrom,
    /// <summary>Explicit date-to — overrides Period when set.</summary>
    DateOnly? DateTo,
    PaginationRequest Pagination,
    // Green-only filters added in v2 (FSD §2.6.7).
    /// <summary>FSD: ประเภทสิ่งปลูกสร้าง — filter masters that have at least one engagement with a matching BuildingTypeCode.</summary>
    string[]? BuildingTypeCodes,
    /// <summary>FSD: ตำบล — exact match on LandDetails.SubDistrict.</summary>
    string? SubDistrict,
    /// <summary>FSD: อำเภอ — exact match on LandDetails.District.</summary>
    string? District,
    /// <summary>FSD: จังหวัด — exact match on LandDetails.Province.</summary>
    string? Province
) : IQuery<HistorySearchResult>;
//
// FSD §2.6.7 fields NOT implemented (deferred):
//   - Old Appraisal Report No — column does not exist
