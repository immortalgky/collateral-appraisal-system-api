using Dapper;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralEngagements.SearchEngagements;

/// <summary>
/// Engagement-grain search backed by vw_CollateralEngagements.
/// Each result row is a distinct past appraisal event (not a master-grain aggregate).
///
/// Filter semantics:
///   CollateralTypes → e.AppraisedCollateralType IN (...) — historically frozen, what was appraised
///   BuildingTypeCodes → EXISTS against CollateralEngagementBuildings — multi-building safe
///   LandArea range → e.LandAreaInSqWa — engagement-time sq.wa
///   TitleDeedNo → LIKE against Land_TitleNumber OR Condo_TitleNumber OR Lh_LeaseRegistrationNo
///   Province/District/SubDistrict → address columns from the view (Land or Condo)
///   Geo → STDistance on Land_GeoPoint OR Condo_GeoPoint (if either falls inside radius)
/// </summary>
public class SearchCollateralEngagementsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<SearchCollateralEngagementsQuery, SearchCollateralEngagementsResult>
{
    // Sort whitelist maps client token → ORDER BY expression
    private static readonly Dictionary<string, string> SortMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["appraisalDate"] = "AppraisalDate DESC",
        ["ownerName"]     = "OwnerName ASC",
    };

    // SELECT columns — order MUST match CollateralEngagementSearchItemDto constructor parameter order.
    // Dapper materializes positional records by column ordinal, not by name.
    private const string SelectColumns = """
        SELECT
            Id,
            CollateralMasterId,
            AppraisalId,
            AppraisalNumber,
            RequestId,
            RequestNumber,
            AppraisalType,
            AppraisalDate,
            AppraiserUserId,
            AppraisalCompanyId,
            AppraisalCompanyName,
            CreatedAt,
            AppraisedCollateralType,
            LandAreaInSqWa,
            AppraisalValue,
            CollateralType,
            OwnerName,
            BuildingTypeCodes,
            Land_Province,
            Land_District,
            Land_SubDistrict,
            Land_TitleNumber,
            Land_Latitude,
            Land_Longitude,
            Condo_Province,
            Condo_TitleNumber,
            Condo_Latitude,
            Condo_Longitude,
            Lh_LeaseRegistrationNo
        """;

    public async Task<SearchCollateralEngagementsResult> Handle(
        SearchCollateralEngagementsQuery query,
        CancellationToken cancellationToken)
    {
        // Visibility (FSD §2.6.7): collateral engagement rows are the green-pin data, which is
        // internal-only — external companies never see it (mirrors HistorySearchQueryHandler,
        // which returns an empty collateral page for external users). Enforced here so every
        // caller of this query inherits the guard, not just the FE drill-down.
        if (currentUser.IsExternal)
        {
            var empty = new PaginatedResult<CollateralEngagementSearchItemDto>(
                [], 0, query.PaginationRequest.PageNumber, query.PaginationRequest.PageSize);
            return new SearchCollateralEngagementsResult(empty);
        }

        var sql = $"{SelectColumns} FROM collateral.vw_CollateralEngagements WHERE 1 = 1";
        var p = new DynamicParameters();

        // --- Appraisal-side filters ---

        if (!string.IsNullOrWhiteSpace(query.AppraisalReportNo))
        {
            sql += " AND AppraisalNumber LIKE @AppraisalReportNo";
            p.Add("AppraisalReportNo", $"%{query.AppraisalReportNo.Trim()}%");
        }

        if (query.AppraisalDateFrom.HasValue)
        {
            sql += " AND AppraisalDate >= @AppraisalDateFrom";
            p.Add("AppraisalDateFrom", query.AppraisalDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.AppraisalDateTo.HasValue)
        {
            sql += " AND AppraisalDate <= @AppraisalDateTo";
            p.Add("AppraisalDateTo", query.AppraisalDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        // --- Collateral-side filters ---

        // CollateralType filter on engagement-time value (historically accurate).
        if (query.CollateralTypes is { Length: > 0 })
        {
            sql += " AND (AppraisedCollateralType IN @CollateralTypes OR (AppraisedCollateralType IS NULL AND CollateralType IN @CollateralTypes))";
            p.Add("CollateralTypes", query.CollateralTypes);
        }

        // Building type filter — EXISTS against child table supports multi-building masters.
        if (query.BuildingTypeCodes is { Length: > 0 })
        {
            sql += " AND EXISTS (SELECT 1 FROM collateral.CollateralEngagementBuildings ceb WHERE ceb.EngagementId = Id AND ceb.BuildingTypeCode IN @BuildingTypeCodes)";
            p.Add("BuildingTypeCodes", query.BuildingTypeCodes);
        }

        // Title deed number — match across Land, Condo, and Leasehold title identifiers.
        if (!string.IsNullOrWhiteSpace(query.TitleDeedNo))
        {
            sql += " AND (Land_TitleNumber LIKE @TitleDeedNo OR Condo_TitleNumber LIKE @TitleDeedNo OR Lh_LeaseRegistrationNo LIKE @TitleDeedNo)";
            p.Add("TitleDeedNo", $"%{query.TitleDeedNo.Trim()}%");
        }

        // Land area range — uses engagement-time sq.wa value.
        if (query.LandAreaFromSqWa.HasValue)
        {
            sql += " AND LandAreaInSqWa >= @LandAreaFrom";
            p.Add("LandAreaFrom", query.LandAreaFromSqWa.Value);
        }

        if (query.LandAreaToSqWa.HasValue)
        {
            sql += " AND LandAreaInSqWa <= @LandAreaTo";
            p.Add("LandAreaTo", query.LandAreaToSqWa.Value);
        }

        // Customer name — LIKE against master OwnerName.
        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND OwnerName LIKE @CustomerName";
            p.Add("CustomerName", $"%{query.CustomerName.Trim()}%");
        }

        // Address filters — match against Land or Condo address columns.
        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            sql += " AND (Land_Province = @Province OR Condo_Province = @Province)";
            p.Add("Province", query.Province.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.District))
        {
            sql += " AND Land_District = @District";
            p.Add("District", query.District.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.SubDistrict))
        {
            sql += " AND Land_SubDistrict = @SubDistrict";
            p.Add("SubDistrict", query.SubDistrict.Trim());
        }

        // Geo-bound circle filter — applies if EITHER Land OR Condo GeoPoint falls within radius.
        // geography::Point is inlined as a literal (not a Dapper parameter) — Dapper without
        // NetTopologySuite cannot bind geography parameters. Coordinates are clamped to WGS-84
        // ranges to prevent SQL injection via numeric literals (same pattern as HistorySearchQueryHandler).
        if (query.CenterLat.HasValue && query.CenterLng.HasValue && query.RadiusKm.HasValue)
        {
            var safeLat = Math.Clamp((double)query.CenterLat.Value, -90.0, 90.0);
            var safeLng = Math.Clamp((double)query.CenterLng.Value, -180.0, 180.0);
            var radiusM = (double)(query.RadiusKm.Value * 1000);
            var center = FormattableString.Invariant($"geography::Point({safeLat:F6}, {safeLng:F6}, 4326)");

            sql += $"""
                 AND (
                     Land_GeoPoint  IS NOT NULL AND Land_GeoPoint.STDistance({center})  <= @RadiusM
                  OR Condo_GeoPoint IS NOT NULL AND Condo_GeoPoint.STDistance({center}) <= @RadiusM
                 )
                """;
            p.Add("RadiusM", radiusM);
        }

        // Drill-down: restrict to a single master (powers "click pin → list its engagements").
        if (query.CollateralMasterId.HasValue)
        {
            sql += " AND CollateralMasterId = @CollateralMasterId";
            p.Add("CollateralMasterId", query.CollateralMasterId.Value);
        }

        var orderBy = SortMap.TryGetValue(query.Sort ?? "", out var col)
            ? col
            : "AppraisalDate DESC";

        var result = await connectionFactory.QueryPaginatedAsync<CollateralEngagementSearchItemDto>(
            sql, orderBy, query.PaginationRequest, p);

        return new SearchCollateralEngagementsResult(result);
    }
}
