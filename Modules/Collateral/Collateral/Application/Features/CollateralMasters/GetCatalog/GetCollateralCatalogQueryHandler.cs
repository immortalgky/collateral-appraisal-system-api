using Dapper;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

/// <summary>
/// Paginated catalog handler. Backed by vw_CollateralMasters via Dapper.
/// Admin-only: enforces IsInRole("Admin") || IsInRole("IntAdmin").
///
/// Supports existing filters (type, province, owner, isUnderConstruction, minAppraisals,
/// lastAppraised date range, sort) plus Phase 1 additions:
///   Types          — multi-select (was single string)
///   TitleNumber    — LIKE against Land and Condo title-number columns
///   District       — exact match on Land_District
///   SubDistrict    — exact match on Land_SubDistrict
///   CompanyId      — EXISTS against collateral.CollateralEngagements
///   Q              — free-text OR across owner/titleNumbers/address/condoName
///   CenterLat/Lng  — geo-bound circle via STDistance on persisted GeoPoint columns (applies to both types)
///   RadiusKm
/// </summary>
public class GetCollateralCatalogQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetCollateralCatalogQuery, GetCollateralCatalogResult>
{
    // Sort whitelist maps client token → view column
    private static readonly Dictionary<string, string> SortMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lastAppraisedDate"]  = "LastAppraisedDate DESC",
        ["ownerName"]          = "OwnerName ASC",
        ["engagementCount"]    = "EngagementCount DESC",
    };

    private const string SelectColumns = """
        SELECT
            Id,
            CollateralType,
            OwnerName,
            CreatedAt,
            ISNULL(EngagementCount, 0)      AS EngagementCount,
            LastAppraisedDate,
            LastAppraisedValue,
            Land_Province,
            Land_District,
            Land_SubDistrict,
            Land_TitleNumber,
            IsUnderConstructionAtLastAppraisal,
            OverallConstructionProgressPercent,
            Condo_Province,
            Condo_CondoName,
            Condo_Latitude,
            Condo_Longitude,
            Land_Latitude,
            Land_Longitude
        """;

    public async Task<GetCollateralCatalogResult> Handle(
        GetCollateralCatalogQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Collateral catalog is restricted to Admin users.");

        var sql = $"{SelectColumns} FROM collateral.vw_CollateralMasters WHERE 1 = 1";
        var p = new DynamicParameters();

        // --- Existing filters ---

        // Multi-select type (Phase 1 widens from single string to array)
        if (query.Types is { Length: > 0 })
        {
            sql += " AND CollateralType IN @Types";
            p.Add("Types", query.Types);
        }

        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            sql += " AND (Land_Province = @Province OR Condo_Province = @Province)";
            p.Add("Province", query.Province.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            sql += " AND OwnerName LIKE @OwnerPattern";
            p.Add("OwnerPattern", "%" + query.Owner.Trim() + "%");
        }

        if (query.IsUnderConstruction.HasValue)
        {
            // Land types after rename: L (bare land), LB (land+building)
            sql += " AND CollateralType IN ('L', 'LB') AND IsUnderConstructionAtLastAppraisal = @IsUnderConstruction";
            p.Add("IsUnderConstruction", query.IsUnderConstruction.Value ? 1 : 0);
        }

        if (query.MinAppraisals.HasValue)
        {
            sql += " AND ISNULL(EngagementCount, 0) >= @MinAppraisals";
            p.Add("MinAppraisals", query.MinAppraisals.Value);
        }

        if (query.LastAppraisedFrom.HasValue)
        {
            sql += " AND LastAppraisedDate >= @LastAppraisedFrom";
            p.Add("LastAppraisedFrom", query.LastAppraisedFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.LastAppraisedTo.HasValue)
        {
            sql += " AND LastAppraisedDate <= @LastAppraisedTo";
            p.Add("LastAppraisedTo", query.LastAppraisedTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        // --- Phase 1 new filters ---

        // TitleNumber: LIKE against Land title number (Condo has no title number)
        if (!string.IsNullOrWhiteSpace(query.TitleNumber))
        {
            sql += " AND Land_TitleNumber LIKE @TitleNumberPattern";
            p.Add("TitleNumberPattern", "%" + query.TitleNumber.Trim() + "%");
        }

        // District: exact match on Land_District
        if (!string.IsNullOrWhiteSpace(query.District))
        {
            sql += " AND Land_District = @District";
            p.Add("District", query.District.Trim());
        }

        // SubDistrict: exact match on Land_SubDistrict
        if (!string.IsNullOrWhiteSpace(query.SubDistrict))
        {
            sql += " AND Land_SubDistrict = @SubDistrict";
            p.Add("SubDistrict", query.SubDistrict.Trim());
        }

        // CompanyId: masters that have at least one engagement by this company.
        // AppraisalCompanyId is uniqueidentifier on CollateralEngagements — parse to Guid so
        // Dapper sends the parameter typed correctly (avoids brittle implicit string→Guid cast).
        if (!string.IsNullOrWhiteSpace(query.CompanyId)
            && Guid.TryParse(query.CompanyId.Trim(), out var companyGuid))
        {
            sql += " AND EXISTS (SELECT 1 FROM collateral.CollateralEngagements e WHERE e.CollateralMasterId = Id AND e.AppraisalCompanyId = @CompanyId)";
            p.Add("CompanyId", companyGuid);
        }

        // Q: free-text OR across owner, land title number, province/district/subDistrict, condoName
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var qPattern = "%" + query.Q.Trim() + "%";
            sql += """
                 AND (
                     OwnerName          LIKE @QPattern
                  OR Land_TitleNumber   LIKE @QPattern
                  OR Land_Province      LIKE @QPattern
                  OR Land_District      LIKE @QPattern
                  OR Land_SubDistrict   LIKE @QPattern
                  OR Condo_Province     LIKE @QPattern
                  OR Condo_CondoName    LIKE @QPattern
                )
                """;
            p.Add("QPattern", qPattern);
        }

        // Geo-bound circle filter using persisted computed GeoPoint columns (STDistance).
        // Applies to a row if EITHER its Land OR Condo GeoPoint falls within the radius.
        // geography::Point is inlined as a literal (not a Dapper parameter) because Dapper
        // without NetTopologySuite cannot bind geography parameters — same approach as
        // HistorySearchQueryHandler.BuildGeoPoint. Coordinates are clamped to WGS-84 ranges
        // to prevent SQL injection via numeric literals.
        // STDistance returns NULL when GeoPoint IS NULL, so NULL rows are filtered out
        // automatically — no explicit NULL check required.
        if (query.CenterLat.HasValue && query.CenterLng.HasValue && query.RadiusKm.HasValue)
        {
            var safeLat = Math.Clamp((double)query.CenterLat.Value, -90.0, 90.0);
            var safeLng = Math.Clamp((double)query.CenterLng.Value, -180.0, 180.0);
            var radiusM = (double)(query.RadiusKm.Value * 1000);
            var center = FormattableString.Invariant($"geography::Point({safeLat:F6}, {safeLng:F6}, 4326)");

            sql += $"""
                 AND (
                     Land_GeoPoint.STDistance({center})  <= @RadiusM
                  OR Condo_GeoPoint.STDistance({center}) <= @RadiusM
                 )
                """;
            p.Add("RadiusM", radiusM);
        }

        var orderBy = SortMap.TryGetValue(query.Sort ?? "", out var col)
            ? col
            : "CreatedAt DESC";

        var result = await connectionFactory.QueryPaginatedAsync<CollateralCatalogItemDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            p);

        return new GetCollateralCatalogResult(result);
    }
}
