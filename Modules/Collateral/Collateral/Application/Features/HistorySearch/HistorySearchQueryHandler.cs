using System.Data;
using Dapper;
using Shared.Identity;

namespace Collateral.Application.Features.HistorySearch;

/// <summary>
/// Executes Dapper queries against collateral.LandDetails (green pins) and
/// appraisal.MarketComparables (blue pins) and combines them into a single
/// HistorySearchResult.
///
/// Visibility rules (enforced server-side — never trust client):
///   Internal user  → BOTH queries run in parallel, each on its own fresh
///                    connection (via CreateNewConnection) to avoid the
///                    "no MARS" error on the scope-shared connection
///   External user  → only MC query runs (reusing the scoped connection);
///                    collateral result is always empty
///   External MC    → filtered to CreatedByCompanyId = currentUser.CompanyId
/// </summary>
public class HistorySearchQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<HistorySearchQuery, HistorySearchResult>
{
    private const decimal MaxRadiusKm = 50m;
    private const int DefaultPageSize = 50;

    public async Task<HistorySearchResult> Handle(
        HistorySearchQuery query,
        CancellationToken cancellationToken)
    {
        // Centre point is optional (FSD §2.6.7). When supplied, build the inline geography
        // literal once; when omitted, `center` stays null and the query methods skip the
        // radius filter + distance ordering.
        var center = query is { CenterLat: { } lat, CenterLon: { } lon }
            ? BuildGeoPoint(lat, lon)
            : null;

        // Server-side radius cap (only meaningful when a centre is given).
        var radiusKm = Math.Min(query.RadiusKm ?? MaxRadiusKm, MaxRadiusKm);

        var isInternal = !currentUser.IsExternal;
        var companyId = currentUser.CompanyId;

        // Resolve the date window from the Period enum (or explicit dates override).
        var (dateFrom, dateTo) = ResolveDateRange(query);

        // Use the pagination from the query, defaulting to page 0 / 50.
        var pagination = query.Pagination with
        {
            PageSize = query.Pagination.PageSize > 0 ? query.Pagination.PageSize : DefaultPageSize
        };

        // Each parallel query needs its own connection. GetOpenConnection() returns
        // a scope-shared instance that doesn't enable MultipleActiveResultSets, so
        // running two Dapper queries against it concurrently throws.
        // CreateNewConnection() pulls a fresh pooled connection per parallel branch.
        if (isInternal)
        {
            using var collateralConn = connectionFactory.CreateNewConnection();
            using var mcConn = connectionFactory.CreateNewConnection();

            var collateralTask = QueryCollateralPinsAsync(
                collateralConn, center, radiusKm, query, dateFrom, dateTo, pagination);
            var mcTask = QueryMarketComparablePinsAsync(
                mcConn, center, radiusKm, query, dateFrom, dateTo, pagination, externalCompanyId: null);

            await Task.WhenAll(collateralTask, mcTask);
            return new HistorySearchResult(await collateralTask, await mcTask);
        }

        // External: collateral is empty, only the MC query runs — reuse the scoped connection.
        var externalMc = await QueryMarketComparablePinsAsync(
            connectionFactory.GetOpenConnection(),
            center, radiusKm, query, dateFrom, dateTo, pagination,
            externalCompanyId: companyId);

        return new HistorySearchResult(EmptyCollateral(pagination), externalMc);
    }

    // ──────────────────────────────────────────────────────────────
    // Collateral query (green pins) — joins LandDetails for GeoPoint
    // ──────────────────────────────────────────────────────────────

    private static async Task<PaginatedResult<CollateralPinDto>> QueryCollateralPinsAsync(
        IDbConnection connection,
        string? center,
        decimal radiusKm,
        HistorySearchQuery query,
        DateTime? dateFrom,
        DateTime? dateTo,
        PaginationRequest pagination)
    {
        // Date filter is applied INSIDE the agg subquery's HAVING so the LEFT JOIN
        // semantics survive — a collateral with no engagements in the window still
        // surfaces (EngagementCount=0, LastAppraisedDate=NULL). Putting the predicate
        // on the outer WHERE would silently turn this into an INNER JOIN.
        var aggDateHaving = (dateFrom, dateTo) switch
        {
            (not null, not null) => " HAVING MAX(AppraisalDate) >= @DateFrom AND MAX(AppraisalDate) <= @DateTo",
            (not null, null)     => " HAVING MAX(AppraisalDate) >= @DateFrom",
            (null,     not null) => " HAVING MAX(AppraisalDate) <= @DateTo",
            _                    => ""
        };

        // Distance column is only computed when a centre is given; otherwise NULL.
        var distanceSelect = center is null
            ? "CAST(NULL AS FLOAT)"
            : $"CAST(ROUND(ld.GeoPoint.STDistance({center}) / 1000.0, 3) AS FLOAT)";

        // Explicit type casts on nullable columns so Dapper's positional record
        // matcher sees the right C# type (untyped NULL defaults to Int32 in ADO.NET,
        // which then fails to bind to `string? PropertyType` on the DTO).
        var sql = $"""
            SELECT
                cm.Id                                                  AS CollateralMasterId,
                ld.Latitude                                            AS Lat,
                ld.Longitude                                           AS Lon,
                cm.CollateralType,
                CAST(NULL AS NVARCHAR(50))                             AS PropertyType,
                ISNULL(agg.EngagementCount, 0)                         AS EngagementCount,
                agg.LastAppraisedDate                                  AS LastAppraisedDate,
                ld.AppraisalValue                                      AS LastAppraisedValue,
                {distanceSelect}                                       AS DistanceKm,
                ld.Province                                            AS Province,
                ld.District                                            AS District,
                ld.SubDistrict                                         AS SubDistrict,
                ld.LastAppraisalNumber                                 AS LastAppraisalNumber
            FROM collateral.CollateralMasters cm
            JOIN collateral.LandDetails ld ON ld.CollateralMasterId = cm.Id
            LEFT JOIN (
                SELECT CollateralMasterId,
                       COUNT(*)           AS EngagementCount,
                       MAX(AppraisalDate) AS LastAppraisedDate
                FROM   collateral.CollateralEngagements
                GROUP BY CollateralMasterId{aggDateHaving}
            ) agg ON agg.CollateralMasterId = cm.Id
            WHERE cm.IsDeleted = 0
              AND cm.IsMaster = 1
              AND ld.GeoPoint IS NOT NULL
            """;

        var p = new DynamicParameters();

        // Radius filter only when a centre point is supplied.
        if (center is not null)
        {
            sql += $" AND ld.GeoPoint.STDistance({center}) <= @RadiusM";
            p.Add("RadiusM", (double)(radiusKm * 1000));
        }

        if (dateFrom.HasValue) p.Add("DateFrom", dateFrom.Value);
        if (dateTo.HasValue)   p.Add("DateTo",   dateTo.Value);

        // ── FSD §2.6.7 collateral-side filters ─────────────────────────────────
        // All optional; LIKE for free-text fields, exact match for enums/ranges.
        if (!string.IsNullOrWhiteSpace(query.AppraisalReportNo))
        {
            sql += " AND ld.LastAppraisalNumber LIKE @AppraisalReportNo";
            p.Add("AppraisalReportNo", $"%{query.AppraisalReportNo.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(query.TitleDeedNo))
        {
            sql += " AND ld.TitleNumber LIKE @TitleDeedNo";
            p.Add("TitleDeedNo", $"%{query.TitleDeedNo.Trim()}%");
        }
        if (query.CollateralTypes is { Length: > 0 })
        {
            sql += " AND cm.CollateralType IN @CollateralTypes";
            p.Add("CollateralTypes", query.CollateralTypes);
        }
        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND cm.OwnerName LIKE @CustomerName";
            p.Add("CustomerName", $"%{query.CustomerName.Trim()}%");
        }
        if (query.LandAreaFromSqWa.HasValue)
        {
            sql += " AND ld.LandArea >= @LandAreaFrom";
            p.Add("LandAreaFrom", query.LandAreaFromSqWa.Value);
        }
        if (query.LandAreaToSqWa.HasValue)
        {
            sql += " AND ld.LandArea <= @LandAreaTo";
            p.Add("LandAreaTo", query.LandAreaToSqWa.Value);
        }
        if (query.ValueFrom.HasValue)
        {
            sql += " AND ld.AppraisalValue >= @ValueFrom";
            p.Add("ValueFrom", query.ValueFrom.Value);
        }
        if (query.ValueTo.HasValue)
        {
            sql += " AND ld.AppraisalValue <= @ValueTo";
            p.Add("ValueTo", query.ValueTo.Value);
        }

        // ── v2 green-only filters ───────────────────────────────────────────────
        // Building type: master must have at least one engagement whose
        // CollateralEngagementBuildings row matches any of the requested codes.
        // EXISTS correlates via CollateralEngagements (CollateralMasterId → engagement)
        // and then into CollateralEngagementBuildings (EngagementId).
        if (query.BuildingTypeCodes is { Length: > 0 })
        {
            sql += """
                 AND EXISTS (
                     SELECT 1
                     FROM   collateral.CollateralEngagements ce
                     JOIN   collateral.CollateralEngagementBuildings ceb ON ceb.EngagementId = ce.Id
                     WHERE  ce.CollateralMasterId = cm.Id
                       AND  ceb.BuildingTypeCode IN @BuildingTypeCodes
                 )
                """;
            p.Add("BuildingTypeCodes", query.BuildingTypeCodes);
        }

        // Address filters — exact match mirroring the engagement-search handler pattern.
        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            sql += " AND ld.Province = @Province";
            p.Add("Province", query.Province.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.District))
        {
            sql += " AND ld.District = @District";
            p.Add("District", query.District.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.SubDistrict))
        {
            sql += " AND ld.SubDistrict = @SubDistrict";
            p.Add("SubDistrict", query.SubDistrict.Trim());
        }

        // Order by distance when a centre is given; otherwise most-recently-appraised first.
        var orderBy = center is not null
            ? $"ld.GeoPoint.STDistance({center}) ASC"
            : "agg.LastAppraisedDate DESC";

        return await connection.QueryPaginatedAsync<CollateralPinDto>(
            sql, orderBy, pagination, p);
    }

    // ──────────────────────────────────────────────────────────────
    // MarketComparable query (blue pins)
    // ──────────────────────────────────────────────────────────────

    private static async Task<PaginatedResult<MarketComparablePinDto>> QueryMarketComparablePinsAsync(
        IDbConnection connection,
        string? center,
        decimal radiusKm,
        HistorySearchQuery query,
        DateTime? dateFrom,
        DateTime? dateTo,
        PaginationRequest pagination,
        Guid? externalCompanyId)
    {
        var distanceSelect = center is null
            ? "CAST(NULL AS FLOAT)"
            : $"CAST(ROUND(mc.GeoPoint.STDistance({center}) / 1000.0, 3) AS FLOAT)";

        var sql = $"""
            SELECT
                mc.Id                                                  AS MarketComparableId,
                mc.Latitude                                            AS Lat,
                mc.Longitude                                           AS Lon,
                mc.PropertyType                                        AS PropertyType,
                mc.SurveyName                                          AS SurveyName,
                mc.InfoDateTime                                        AS InfoDateTime,
                mc.OfferPrice                                          AS OfferPrice,
                mc.SalePrice                                           AS SalePrice,
                {distanceSelect}                                       AS DistanceKm,
                (
                    SELECT TOP 1 a.AppraisalNumber
                    FROM appraisal.AppraisalComparables ac
                    JOIN appraisal.Appraisals a ON a.Id = ac.AppraisalId
                    WHERE ac.MarketComparableId = mc.Id
                    ORDER BY a.CompletedAt DESC, a.Id DESC
                )                                                      AS AppraisalNumber
            FROM appraisal.MarketComparables mc
            WHERE mc.IsDeleted = 0
              AND mc.GeoPoint IS NOT NULL
            """;

        var p = new DynamicParameters();

        // Radius filter only when a centre point is supplied.
        if (center is not null)
        {
            sql += $" AND mc.GeoPoint.STDistance({center}) <= @RadiusM";
            p.Add("RadiusM", (double)(radiusKm * 1000));
        }

        // External users see only their own company's records.
        if (externalCompanyId.HasValue)
        {
            sql += " AND mc.CreatedByCompanyId = @CompanyId";
            p.Add("CompanyId", externalCompanyId.Value);
        }

        // MC pins are filtered ONLY by lat/lon + radius + period (date window).
        // Business filters (CollateralType, AppraisalReportNo, TitleDeedNo,
        // CustomerName, LandArea, Value) apply to green pins only — they have
        // no equivalent on MarketComparable.
        if (dateFrom.HasValue)
        {
            sql += " AND mc.InfoDateTime >= @DateFrom";
            p.Add("DateFrom", dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            sql += " AND mc.InfoDateTime <= @DateTo";
            p.Add("DateTo", dateTo.Value);
        }

        // Order by distance when a centre is given; otherwise newest survey first.
        var orderBy = center is not null
            ? $"mc.GeoPoint.STDistance({center}) ASC"
            : "mc.InfoDateTime DESC";

        return await connection.QueryPaginatedAsync<MarketComparablePinDto>(
            sql, orderBy, pagination, p);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Produces a safe inline geography::Point expression for use inside SQL strings.
    /// Using parameterized STDistance(@center) would require a geography parameter which
    /// Dapper doesn't support without NetTopologySuite, so we inline the literal.
    /// The values are validated to be in range before use.
    /// </summary>
    private static string BuildGeoPoint(decimal lat, decimal lon)
    {
        // Clamp to valid WGS-84 ranges to prevent SQL injection via numeric literals.
        var safeLat = Math.Clamp((double)lat, -90.0, 90.0);
        var safeLon = Math.Clamp((double)lon, -180.0, 180.0);
        return FormattableString.Invariant(
            $"geography::Point({safeLat:F6}, {safeLon:F6}, 4326)");
    }

    // Bangkok timezone — InfoDateTime / AppraisalDate are stored as local Bangkok
    // time (DateTime, no offset). Using UtcNow here produced a year-boundary
    // off-by-week bug each January for early-morning records.
    private static readonly TimeZoneInfo BangkokTz =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Bangkok");

    private static (DateTime? dateFrom, DateTime? dateTo) ResolveDateRange(HistorySearchQuery query)
    {
        // Explicit dates override the Period.
        if (query.DateFrom.HasValue || query.DateTo.HasValue)
        {
            var from = query.DateFrom?.ToDateTime(TimeOnly.MinValue);
            var to   = query.DateTo?.ToDateTime(TimeOnly.MaxValue);
            return (from, to);
        }

        var nowBkk = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BangkokTz);
        return query.Period switch
        {
            Period.Past3y  => (nowBkk.AddYears(-3), null),
            Period.Past2y  => (nowBkk.AddYears(-2), null),
            Period.Past1y  => (nowBkk.AddYears(-1), null),
            Period.Current => (new DateTime(nowBkk.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), null),
            _              => (nowBkk.AddYears(-3), null)
        };
    }

    private static PaginatedResult<CollateralPinDto> EmptyCollateral(PaginationRequest pagination) =>
        new([], 0, pagination.PageNumber, pagination.PageSize);
}
