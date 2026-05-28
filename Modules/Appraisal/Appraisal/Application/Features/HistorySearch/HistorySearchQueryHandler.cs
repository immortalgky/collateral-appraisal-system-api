using System.Data;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.HistorySearch;

/// <summary>
/// Executes Dapper queries against appraisal.LandAppraisalDetails /
/// appraisal.CondoAppraisalDetails (green appraisal pins) and
/// appraisal.MarketComparables (blue pins) and combines them into a single
/// HistorySearchResult.
///
/// Visibility rules (enforced server-side — never trust client):
///   Internal user  → BOTH queries run in parallel, each on its own fresh
///                    connection (via CreateNewConnection) to avoid the
///                    "no MARS" error on the scope-shared connection.
///   External user  → only MC query runs (reusing the scoped connection);
///                    appraisal result is always empty.
///   External MC    → filtered to CreatedByCompanyId = currentUser.CompanyId.
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
            using var appraisalConn = connectionFactory.CreateNewConnection();
            using var mcConn = connectionFactory.CreateNewConnection();

            var appraisalTask = QueryAppraisalPinsAsync(
                appraisalConn, center, radiusKm, query, dateFrom, dateTo, pagination);
            var mcTask = QueryMarketComparablePinsAsync(
                mcConn, center, radiusKm, query, dateFrom, dateTo, pagination, externalCompanyId: null);

            await Task.WhenAll(appraisalTask, mcTask);
            return new HistorySearchResult(await appraisalTask, await mcTask);
        }

        // External: appraisals are empty, only the MC query runs — reuse the scoped connection.
        var externalMc = await QueryMarketComparablePinsAsync(
            connectionFactory.GetOpenConnection(),
            center, radiusKm, query, dateFrom, dateTo, pagination,
            externalCompanyId: companyId);

        return new HistorySearchResult(EmptyAppraisals(pagination), externalMc);
    }

    // ──────────────────────────────────────────────────────────────
    // Appraisal query (green pins)
    //
    // One row per completed Appraisal. Representative location chosen via:
    //   - When a centre is given: CROSS APPLY picks the property whose GeoPoint
    //     is nearest the search centre (uses the spatial index on each detail table).
    //   - When no centre: lowest SequenceNumber property that has a non-null GeoPoint.
    //
    // Both LandAppraisalDetails and CondoAppraisalDetails are considered as candidate
    // coordinate sources (UNION ALL). The spatial index on GeoPoint in each table
    // enables efficient radius filtering.
    // ──────────────────────────────────────────────────────────────

    private static async Task<PaginatedResult<AppraisalPinDto>> QueryAppraisalPinsAsync(
        IDbConnection connection,
        string? center,
        decimal radiusKm,
        HistorySearchQuery query,
        DateTime? dateFrom,
        DateTime? dateTo,
        PaginationRequest pagination)
    {
        // Distance column: computed from the chosen representative point.
        var distanceSelect = center is null
            ? "CAST(NULL AS FLOAT)"
            : $"CAST(ROUND(rep.GeoPoint.STDistance({center}) / 1000.0, 3) AS FLOAT)";

        // Representative-point sub-query depends on whether a centre is given.
        // Both branches union LandAppraisalDetails and CondoAppraisalDetails.
        // The GeoPoint column is the PERSISTED computed column added by the migration —
        // querying it applies the spatial index and avoids full-table scans.
        string repApply;
        if (center is not null)
        {
            // Nearest property to the search centre, with GeoPoint inside the radius.
            repApply = $"""
                CROSS APPLY (
                    SELECT TOP 1
                        d.Latitude,
                        d.Longitude,
                        d.GeoPoint,
                        d.SubDistrict,
                        d.District,
                        d.Province,
                        d.PropertyType,
                        d.BuildingType
                    FROM (
                        SELECT
                            ld.Latitude, ld.Longitude, ld.GeoPoint,
                            ld.SubDistrict, ld.District, ld.Province,
                            ap2.PropertyType,
                            (SELECT TOP 1 bad.BuildingType
                             FROM appraisal.BuildingAppraisalDetails bad
                             WHERE bad.AppraisalPropertyId = ap2.Id) AS BuildingType
                        FROM appraisal.LandAppraisalDetails ld
                        JOIN appraisal.AppraisalProperties ap2
                            ON ap2.Id = ld.AppraisalPropertyId
                        WHERE ap2.AppraisalId = a.Id
                          AND ld.GeoPoint IS NOT NULL
                          AND ld.GeoPoint.STDistance({center}) <= @RadiusM
                        UNION ALL
                        SELECT
                            cd.Latitude, cd.Longitude, cd.GeoPoint,
                            cd.SubDistrict, cd.District, cd.Province,
                            ap2.PropertyType,
                            CAST(NULL AS NVARCHAR(100)) AS BuildingType
                        FROM appraisal.CondoAppraisalDetails cd
                        JOIN appraisal.AppraisalProperties ap2
                            ON ap2.Id = cd.AppraisalPropertyId
                        WHERE ap2.AppraisalId = a.Id
                          AND cd.GeoPoint IS NOT NULL
                          AND cd.GeoPoint.STDistance({center}) <= @RadiusM
                    ) d
                    ORDER BY d.GeoPoint.STDistance({center}) ASC
                ) rep
                """;
        }
        else
        {
            // Deterministic: lowest-sequence-number property with a non-null GeoPoint.
            repApply = """
                CROSS APPLY (
                    SELECT TOP 1
                        d.Latitude,
                        d.Longitude,
                        d.GeoPoint,
                        d.SubDistrict,
                        d.District,
                        d.Province,
                        d.PropertyType,
                        d.BuildingType
                    FROM (
                        SELECT
                            ld.Latitude, ld.Longitude, ld.GeoPoint,
                            ld.SubDistrict, ld.District, ld.Province,
                            ap2.PropertyType,
                            (SELECT TOP 1 bad.BuildingType
                             FROM appraisal.BuildingAppraisalDetails bad
                             WHERE bad.AppraisalPropertyId = ap2.Id) AS BuildingType,
                            ap2.SequenceNumber
                        FROM appraisal.LandAppraisalDetails ld
                        JOIN appraisal.AppraisalProperties ap2
                            ON ap2.Id = ld.AppraisalPropertyId
                        WHERE ap2.AppraisalId = a.Id
                          AND ld.GeoPoint IS NOT NULL
                        UNION ALL
                        SELECT
                            cd.Latitude, cd.Longitude, cd.GeoPoint,
                            cd.SubDistrict, cd.District, cd.Province,
                            ap2.PropertyType,
                            CAST(NULL AS NVARCHAR(100)) AS BuildingType,
                            ap2.SequenceNumber
                        FROM appraisal.CondoAppraisalDetails cd
                        JOIN appraisal.AppraisalProperties ap2
                            ON ap2.Id = cd.AppraisalPropertyId
                        WHERE ap2.AppraisalId = a.Id
                          AND cd.GeoPoint IS NOT NULL
                    ) d
                    ORDER BY d.SequenceNumber ASC
                ) rep
                """;
        }

        // vw_AppraisalList provides: AppraisalNumber, CustomerName, AppraisalValue,
        // AssigneeCompanyId, AppointmentDateTime. The view already filters IsDeleted = 0.
        //
        // "Appraisal Date" = the appointment date (when the property was inspected),
        // NOT the completion date. Falls back to CompletedAt when an appraisal has no
        // appointment so completed appraisals are never silently dropped.
        var sql = $"""
            SELECT
                a.Id                                                   AS AppraisalId,
                al.AppraisalNumber                                     AS AppraisalNumber,
                rep.Latitude                                           AS Lat,
                rep.Longitude                                          AS Lon,
                rep.PropertyType                                       AS PropertyType,
                rep.BuildingType                                       AS BuildingType,
                al.AppraisalValue                                      AS AppraisedValue,
                COALESCE(al.AppointmentDateTime, a.CompletedAt)        AS AppraisedDate,
                {distanceSelect}                                       AS DistanceKm,
                rep.Province                                           AS Province,
                rep.District                                           AS District,
                rep.SubDistrict                                        AS SubDistrict,
                al.CustomerName                                        AS CustomerName
            FROM appraisal.Appraisals a
            JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
            {repApply}
            WHERE a.CompletedAt IS NOT NULL
            """;

        // NOTE: `a.IsDeleted = 0` is enforced by vw_AppraisalList (its WHERE clause).
        // Joining through the view means we do NOT need a redundant IsDeleted filter here.

        var p = new DynamicParameters();

        // Radius filter parameter — passed once; inline literal handles the spatial comparison.
        if (center is not null)
        {
            p.Add("RadiusM", (double)(radiusKm * 1000));
        }

        // Date + business + address criteria. Shared with the MC "linked" branch
        // (QueryMarketComparablePinsAsync) so blue pins follow the same matched appraisals.
        AppendAppraisalFilters(ref sql, p, query, dateFrom, dateTo);

        // Order by distance when a centre is given; otherwise most-recent appraisal date first.
        var orderBy = center is not null
            ? $"rep.GeoPoint.STDistance({center}) ASC"
            : "COALESCE(al.AppointmentDateTime, a.CompletedAt) DESC";

        return await connection.QueryPaginatedAsync<AppraisalPinDto>(
            sql, orderBy, pagination, p);
    }

    /// <summary>
    /// Appends the appraisal date-window + business + address criteria to <paramref name="sql"/>,
    /// referencing aliases <c>a</c> (appraisal.Appraisals) and <c>al</c> (vw_AppraisalList).
    /// Used both at the top level of the appraisal query AND inside the MC "linked" EXISTS
    /// (where the same <c>a</c>/<c>al</c> aliases are in scope) so blue pins follow the same
    /// matched appraisals. The radius filter is NOT included here — it lives in the appraisal
    /// rep CROSS APPLY and the MC geographic branch respectively.
    /// </summary>
    private static void AppendAppraisalFilters(
        ref string sql,
        DynamicParameters p,
        HistorySearchQuery query,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        // Date window filters on the appraisal date (appointment date, falling back to
        // CompletedAt) — consistent with the displayed "Appraisal Date".
        if (dateFrom.HasValue)
        {
            sql += " AND COALESCE(al.AppointmentDateTime, a.CompletedAt) >= @DateFrom";
            p.Add("DateFrom", dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            sql += " AND COALESCE(al.AppointmentDateTime, a.CompletedAt) <= @DateTo";
            p.Add("DateTo", dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.AppraisalReportNo))
        {
            sql += " AND al.AppraisalNumber LIKE @AppraisalReportNo";
            p.Add("AppraisalReportNo", $"%{query.AppraisalReportNo.Trim()}%");
        }

        // TitleDeedNo: match on any LandTitle or on CondoAppraisalDetail.TitleNumber.
        if (!string.IsNullOrWhiteSpace(query.TitleDeedNo))
        {
            sql += """
                 AND (
                     EXISTS (
                         SELECT 1
                         FROM appraisal.LandTitles lt
                         JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
                         JOIN appraisal.AppraisalProperties ap3 ON ap3.Id = lad.AppraisalPropertyId
                         WHERE ap3.AppraisalId = a.Id
                           AND lt.TitleNumber LIKE @TitleDeedNo
                     )
                     OR EXISTS (
                         SELECT 1
                         FROM appraisal.CondoAppraisalDetails cad
                         JOIN appraisal.AppraisalProperties ap3 ON ap3.Id = cad.AppraisalPropertyId
                         WHERE ap3.AppraisalId = a.Id
                           AND cad.TitleNumber LIKE @TitleDeedNo
                     )
                 )
                """;
            p.Add("TitleDeedNo", $"%{query.TitleDeedNo.Trim()}%");
        }

        // CollateralTypes: match on AppraisalProperties.PropertyType code(s).
        if (query.CollateralTypes is { Length: > 0 })
        {
            sql += """
                 AND EXISTS (
                     SELECT 1
                     FROM appraisal.AppraisalProperties ap4
                     WHERE ap4.AppraisalId = a.Id
                       AND ap4.PropertyType IN @CollateralTypes
                 )
                """;
            p.Add("CollateralTypes", query.CollateralTypes);
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND al.CustomerName LIKE @CustomerName";
            p.Add("CustomerName", $"%{query.CustomerName.Trim()}%");
        }

        // LandArea: total sq.wa = AreaRai*400 + AreaNgan*100 + AreaSquareWa, summed across all titles.
        // Filters appraisals where at least one LandTitle falls in the range.
        // TODO: the filter compares each individual title's area to the range, not the sum.
        //       If the requirement is sum-of-titles-per-detail, this would need an additional sub-aggregate.
        //       For now, any-title-in-range semantics match the collateral handler's pattern.
        if (query.LandAreaFromSqWa.HasValue || query.LandAreaToSqWa.HasValue)
        {
            sql += """
                 AND EXISTS (
                     SELECT 1
                     FROM appraisal.LandTitles lt2
                     JOIN appraisal.LandAppraisalDetails lad2 ON lad2.Id = lt2.LandAppraisalDetailId
                     JOIN appraisal.AppraisalProperties ap5 ON ap5.Id = lad2.AppraisalPropertyId
                     WHERE ap5.AppraisalId = a.Id
                """;
            if (query.LandAreaFromSqWa.HasValue)
            {
                sql += " AND (lt2.AreaRai * 400 + lt2.AreaNgan * 100 + lt2.AreaSquareWa) >= @LandAreaFrom";
                p.Add("LandAreaFrom", query.LandAreaFromSqWa.Value);
            }
            if (query.LandAreaToSqWa.HasValue)
            {
                sql += " AND (lt2.AreaRai * 400 + lt2.AreaNgan * 100 + lt2.AreaSquareWa) <= @LandAreaTo";
                p.Add("LandAreaTo", query.LandAreaToSqWa.Value);
            }
            sql += " )";
        }

        // Value range: AppraisalValue from vw_AppraisalList (sourced from ValuationAnalyses).
        if (query.ValueFrom.HasValue)
        {
            sql += " AND al.AppraisalValue >= @ValueFrom";
            p.Add("ValueFrom", query.ValueFrom.Value);
        }
        if (query.ValueTo.HasValue)
        {
            sql += " AND al.AppraisalValue <= @ValueTo";
            p.Add("ValueTo", query.ValueTo.Value);
        }

        // BuildingTypeCodes: appraisal must have at least one building property with matching BuildingType.
        // BuildingAppraisalDetails.BuildingType stores the building type code.
        if (query.BuildingTypeCodes is { Length: > 0 })
        {
            sql += """
                 AND EXISTS (
                     SELECT 1
                     FROM appraisal.BuildingAppraisalDetails bad
                     JOIN appraisal.AppraisalProperties ap6 ON ap6.Id = bad.AppraisalPropertyId
                     WHERE ap6.AppraisalId = a.Id
                       AND bad.BuildingType IN @BuildingTypeCodes
                 )
                """;
            p.Add("BuildingTypeCodes", query.BuildingTypeCodes);
        }

        // Address filters: match if ANY property of the appraisal (land or condo)
        // is at the requested location — NOT just the representative property.
        // An appraisal can span multiple properties in different administrative
        // areas; filtering on the representative (nearest) detail alone would wrongly
        // drop appraisals whose matching property isn't the nearest one. EXISTS over
        // both detail tables mirrors the other multi-property filters above.
        AppendAddressFilter(ref sql, p, "Province", query.Province);
        AppendAddressFilter(ref sql, p, "District", query.District);
        AppendAddressFilter(ref sql, p, "SubDistrict", query.SubDistrict);
    }

    /// <summary>
    /// Appends an "appraisal has any land/condo property in this administrative area"
    /// EXISTS predicate. <paramref name="column"/> is a fixed whitelist literal
    /// (Province/District/SubDistrict) from our own code — never user input — so the
    /// interpolation is injection-safe; the value is a bound parameter.
    /// </summary>
    private static void AppendAddressFilter(
        ref string sql, DynamicParameters p, string column, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        sql += $"""
             AND (
                 EXISTS (
                     SELECT 1
                     FROM appraisal.LandAppraisalDetails lad7
                     JOIN appraisal.AppraisalProperties ap7 ON ap7.Id = lad7.AppraisalPropertyId
                     WHERE ap7.AppraisalId = a.Id AND lad7.{column} = @{column}
                 )
                 OR EXISTS (
                     SELECT 1
                     FROM appraisal.CondoAppraisalDetails cad7
                     JOIN appraisal.AppraisalProperties ap8 ON ap8.Id = cad7.AppraisalPropertyId
                     WHERE ap8.AppraisalId = a.Id AND cad7.{column} = @{column}
                 )
             )
            """;
        p.Add(column, value.Trim());
    }

    // ──────────────────────────────────────────────────────────────
    // MarketComparable query (blue pins). Two modes:
    //   • Centre given  → geographic: ALL MCs within the radius (+ own date window).
    //   • No centre     → linked: comparables of the appraisals matching the criteria
    //                     (shares AppendAppraisalFilters with the appraisal query).
    // External users are scoped to CreatedByCompanyId in both modes.
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
                )                                                      AS AppraisalNumber,
                (
                    SELECT TOP 1 cust.Name
                    FROM appraisal.AppraisalComparables ac
                    JOIN appraisal.Appraisals a ON a.Id = ac.AppraisalId
                    OUTER APPLY (
                        SELECT TOP 1 Name
                        FROM request.RequestCustomers
                        WHERE RequestId = a.RequestId
                    ) cust
                    WHERE ac.MarketComparableId = mc.Id
                    ORDER BY a.CompletedAt DESC, a.Id DESC
                )                                                      AS CustomerName,
                (
                    SELECT TOP 1 COALESCE(al2.AppointmentDateTime, a.CompletedAt)
                    FROM appraisal.AppraisalComparables ac
                    JOIN appraisal.Appraisals a ON a.Id = ac.AppraisalId
                    JOIN appraisal.vw_AppraisalList al2 ON al2.Id = a.Id
                    WHERE ac.MarketComparableId = mc.Id
                    ORDER BY a.CompletedAt DESC, a.Id DESC
                )                                                      AS AppraisalDate
            FROM appraisal.MarketComparables mc
            WHERE mc.IsDeleted = 0
              AND mc.GeoPoint IS NOT NULL
            """;

        var p = new DynamicParameters();

        // External users see only their own company's records (both modes).
        if (externalCompanyId.HasValue)
        {
            sql += " AND mc.CreatedByCompanyId = @CompanyId";
            p.Add("CompanyId", externalCompanyId.Value);
        }

        string orderBy;
        if (center is not null)
        {
            // ── Radius (geographic) mode ──────────────────────────────────────
            // Centre given → show ALL market comparables within the radius, filtered by
            // their own survey date window. MCs are NOT tied to matched appraisals here
            // (a radius search is a pure geographic lookup). Business criteria have no
            // equivalent on MarketComparable, so they do not apply in this mode.
            sql += $" AND mc.GeoPoint.STDistance({center}) <= @RadiusM";
            p.Add("RadiusM", (double)(radiusKm * 1000));

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

            orderBy = $"mc.GeoPoint.STDistance({center}) ASC";
        }
        else
        {
            // ── Attribute (linked) mode ───────────────────────────────────────
            // No centre → the search criteria drive the appraisal match, and the blue
            // pins are the comparables LINKED (via AppraisalComparables) to those matched
            // appraisals. The date window + business + address criteria are applied to the
            // appraisal inside the EXISTS (NOT to mc.InfoDateTime), so every comparable of a
            // matched appraisal is shown regardless of its own survey date.
            sql += """
                 AND EXISTS (
                     SELECT 1
                     FROM appraisal.AppraisalComparables ac
                     JOIN appraisal.Appraisals a ON a.Id = ac.AppraisalId
                     JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
                     WHERE ac.MarketComparableId = mc.Id
                       AND a.CompletedAt IS NOT NULL
                """;
            AppendAppraisalFilters(ref sql, p, query, dateFrom, dateTo);
            sql += " )";

            orderBy = "mc.InfoDateTime DESC";
        }

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
    /// The values are clamped to valid WGS-84 ranges to prevent SQL injection via numeric literals.
    /// </summary>
    private static string BuildGeoPoint(decimal lat, decimal lon)
    {
        var safeLat = Math.Clamp((double)lat, -90.0, 90.0);
        var safeLon = Math.Clamp((double)lon, -180.0, 180.0);
        return FormattableString.Invariant(
            $"geography::Point({safeLat:F6}, {safeLon:F6}, 4326)");
    }

    // Bangkok timezone — CompletedAt / InfoDateTime are stored as local Bangkok
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

    private static PaginatedResult<AppraisalPinDto> EmptyAppraisals(PaginationRequest pagination) =>
        new([], 0, pagination.PageNumber, pagination.PageSize);
}
