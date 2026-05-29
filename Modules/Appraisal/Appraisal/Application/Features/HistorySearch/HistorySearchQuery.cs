namespace Appraisal.Application.Features.HistorySearch;

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
/// Returns Appraisal pins (green) and MarketComparable pins (blue). When a centre point
/// (CenterLat/CenterLon) is supplied, results are filtered to RadiusKm and ordered
/// by distance. When the centre is omitted, the radius filter and distance ordering
/// are skipped and results are ordered by date.
/// </summary>
public record HistorySearchQuery(
    /// <summary>Centre latitude. Optional — when null, no radius filter / distance sort.</summary>
    decimal? CenterLat,
    /// <summary>Centre longitude. Optional — when null, no radius filter / distance sort.</summary>
    decimal? CenterLon,
    /// <summary>Radius in km — capped server-side at 50 km. Only applied when a centre is given.</summary>
    decimal? RadiusKm,
    Period Period,
    // Green-only filters (apply only to Appraisal pins; MC pins ignore these).
    /// <summary>FSD: หมายเลขเล่มประเมิน — LIKE on Appraisals.AppraisalNumber.</summary>
    string? AppraisalReportNo,
    /// <summary>FSD: เลขที่โฉนด — LIKE on LandTitles.TitleNumber or CondoAppraisalDetails.TitleNumber.</summary>
    string? TitleDeedNo,
    /// <summary>
    /// FSD: ประเภทหลักประกัน — match on AppraisalProperties.PropertyType code(s).
    /// Codes: L, LB, U, LSL, LSB, LS, MAC, B, VEH, VES, LSU.
    /// </summary>
    string[]? CollateralTypes,
    /// <summary>FSD: ชื่อลูกค้า — LIKE on request.RequestCustomers.Name.</summary>
    string? CustomerName,
    /// <summary>FSD: เนื้อที่ดิน Sq.Wa — total sq.wa of land titles (AreaRai*400 + AreaNgan*100 + AreaSquareWa).</summary>
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
    // Green-only filters (FSD §2.6.7).
    /// <summary>FSD: ประเภทสิ่งปลูกสร้าง — filter appraisals with at least one building property of matching type.</summary>
    string[]? BuildingTypeCodes,
    /// <summary>FSD: ตำบล — exact match on representative detail address SubDistrict.</summary>
    string? SubDistrict,
    /// <summary>FSD: อำเภอ — exact match on representative detail address District.</summary>
    string? District,
    /// <summary>FSD: จังหวัด — exact match on representative detail address Province.</summary>
    string? Province
) : IQuery<HistorySearchResult>;
