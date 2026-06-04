using Dapper;

namespace Request.Application.Features.Reappraisal.GetCandidateById;

/// <summary>
/// Read-side handler for the Reappraisal Candidate detail page.
///
/// Returns the candidate's full detail plus a list of nearby appraisals/candidates
/// within <c>RadiusKm</c> (default 1 km) for the "Group Appraisal" selection table.
///
/// Nearby list draws from TWO sources and merges them:
///   1. In-system appraisals from <c>appraisal.vw_AppraisalList</c> with coords via
///      CROSS APPLY (same pattern as <c>As400ReappraisalJob.FetchCoordinatesAsync</c>).
///   2. Pending candidates from <c>request.ReappraisalCandidates</c> with a non-null GeoPoint.
///
/// When both sides share the same AppraisalNumber / SurveyNumber they collapse into one row
/// that carries both AppraisalId and CandidateId, with Source='Candidate'.
///
/// Geo radius uses <c>STDistance</c> against inline geography::Point literals following the
/// same safe clamping approach as <c>HistorySearchQueryHandler.BuildGeoPoint</c>.
/// </summary>
public class GetReappraisalCandidateByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetReappraisalCandidateByIdQuery, GetReappraisalCandidateByIdResult?>
{
    public async Task<GetReappraisalCandidateByIdResult?> Handle(
        GetReappraisalCandidateByIdQuery query,
        CancellationToken cancellationToken)
    {
        using var conn = connectionFactory.CreateNewConnection();

        // ── Detail query ────────────────────────────────────────────────────────
        const string detailSql = """
            SELECT
                c.Id,
                c.Status,
                c.ReviewType,
                c.AppraisalDate,
                c.DaysSinceLastAppraisal,
                c.RemainingDay,
                c.OldAppraisalReportNumber,
                c.CifNumber,
                c.CustomerName,
                c.CollateralId,
                c.CollateralName,
                c.CollateralAddress,
                c.CollateralCode,
                c.CollateralCategory,
                c.CollateralDescription,
                c.CurrentValue,
                c.ValuationDate,
                c.AoCode,
                c.AoName,
                c.TitleNumber,
                c.InternalExternal,
                c.BusinessSize,
                c.BusinessSizeDesc,
                c.MortgageAmount,
                c.PastDueDay,
                c.ApplicationNumber,
                c.FacilityCode,
                c.FacilityLimit,
                c.CarCode,
                c.SllOver100M,
                c.SllDescription,
                c.Stage,
                c.IBGRetail,
                c.[Group],
                c.EffectiveDateAppraisal,
                c.FlagLessAge4Y,
                c.FlagGreaterAge4Y,
                c.CountAgeingDate,
                c.ExternalValuerName,
                c.InternalValuerName,
                c.Latitude,
                c.Longitude,
                c.HasOpenAppraisal,
                c.OpenAppraisalId,
                c.OpenAppraisalNumber,
                c.OpenAppraisalGroupTag
            FROM request.vw_ReappraisalCandidates c
            WHERE c.Id = @Id
            """;

        var detail = await conn.QueryFirstOrDefaultAsync<ReappraisalCandidateDetail>(
            detailSql, new { query.Id });

        if (detail is null) return null;

        // Resolve the main candidate's own in-system AppraisalId. Returns Guid.Empty
        // when no match — safe to compare, no appraisal has that ID. Exposed on the
        // DTO (as the real appraisal id, NULL when unmatched) and reused below to
        // exclude the candidate's own appraisal from the nearby list.
        var selfAppraisalId = await ResolveSelfAppraisalIdAsync(
            conn, detail.OldAppraisalReportNumber, cancellationToken);
        detail.AppraisalId = selfAppraisalId == Guid.Empty ? null : selfAppraisalId;

        // ── Nearby group candidates query ────────────────────────────────────────
        // Inline geography::Point literals (same safe-clamping as HistorySearchQueryHandler).
        if (detail.Latitude.HasValue && detail.Longitude.HasValue)
        {
            var center = BuildGeoPoint(detail.Latitude.Value, detail.Longitude.Value);
            var radiusM = (double)(query.RadiusKm * 1000);

            // The nearby query is a FULL OUTER JOIN between two sources:
            //   AppraisalCoords CTE = in-system appraisals from vw_AppraisalList + coord CROSS APPLY
            //   CandidateCoords CTE = Pending COLLATREV candidates with a persisted GeoPoint
            // Joined on AppraisalNumber = SurveyNumber.
            // Source='Candidate' when a Pending candidate row exists; else 'InSystem'.
            //
            // Exclusions (outer WHERE):
            //   - The main candidate itself
            //   - Its underlying appraisal (when resolvable)
            //   - Rows already in-flight: a non-terminal reappraisal Request references
            //     the same appraisal via request.RequestDetails.PrevAppraisalId.
            var nearbySql = $"""
                WITH AppraisalCoords AS (
                    SELECT
                        a.Id              AS AppraisalId,
                        a.AppraisalNumber,
                        al.CustomerName,
                        al.AppointmentDateTime,
                        CAST(d.Latitude   AS decimal(10,7)) AS Latitude,
                        CAST(d.Longitude  AS decimal(10,7)) AS Longitude,
                        geography::Point(
                            CAST(d.Latitude  AS float),
                            CAST(d.Longitude AS float),
                            4326
                        ) AS GeoPoint
                    FROM appraisal.Appraisals a
                    JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
                    CROSS APPLY (
                        SELECT TOP 1 u.Latitude, u.Longitude
                        FROM (
                            SELECT TOP 1 ld.Latitude, ld.Longitude, 1 AS Pref
                            FROM appraisal.LandAppraisalDetails ld
                            JOIN appraisal.AppraisalProperties ap ON ap.Id = ld.AppraisalPropertyId
                            WHERE ap.AppraisalId = a.Id
                              AND ld.Latitude IS NOT NULL AND ld.Longitude IS NOT NULL
                            UNION ALL
                            SELECT TOP 1 cd.Latitude, cd.Longitude, 2 AS Pref
                            FROM appraisal.CondoAppraisalDetails cd
                            JOIN appraisal.AppraisalProperties ap ON ap.Id = cd.AppraisalPropertyId
                            WHERE ap.AppraisalId = a.Id
                              AND cd.Latitude IS NOT NULL AND cd.Longitude IS NOT NULL
                        ) u
                        ORDER BY u.Pref
                    ) d
                    WHERE {center}.STDistance(
                              geography::Point(CAST(d.Latitude AS float), CAST(d.Longitude AS float), 4326)
                          ) <= @RadiusM
                      -- Nearby = IBG banking segment only.
                      AND a.BankingSegment = 'IBG'
                      -- Only completed in-system appraisals are eligible for the nearby/group list.
                      AND a.Status = 'Completed'
                ),
                CandidateCoords AS (
                    SELECT rc.Id, rc.SurveyNumber, rc.GeoPoint,
                           rc.CifName, rc.ReviewDate, rc.ReviewType, rc.CurrentValue,
                           rc.Latitude, rc.Longitude
                    FROM request.ReappraisalCandidates rc
                    WHERE rc.Status = 'Pending'
                      AND rc.GeoPoint IS NOT NULL
                      AND {center}.STDistance(rc.GeoPoint) <= @RadiusM
                      -- Nearby = IBG banking segment only (candidate-source).
                      AND rc.IBGRetail = 'IBG'
                )
                SELECT
                    appl.AppraisalId,
                    cand.Id                                                                     AS CandidateId,
                    CASE WHEN cand.Id IS NOT NULL THEN 'Candidate' ELSE 'InSystem' END          AS Source,
                    COALESCE(cand.SurveyNumber,   appl.AppraisalNumber)                         AS OldAppraisalReportNumber,
                    COALESCE(cand.CifName,         appl.CustomerName)                           AS CustomerName,
                    cand.CurrentValue,
                    -- All three derive from the matched in-system appraisal's AppointmentDateTime:
                    --   AppraisalDate            = AppointmentDateTime
                    --   RemainingDay             = (AppointmentDateTime + 5 years) − today
                    --   DaysSinceLastAppraisal   = today − AppointmentDateTime
                    -- Candidate-only rows (no in-system match) → all three NULL.
                    CAST(appl.AppointmentDateTime AS date)                                        AS AppraisalDate,
                    DATEDIFF(DAY,
                        CAST(GETDATE() AS date),
                        DATEADD(YEAR, 5, CAST(appl.AppointmentDateTime AS date))
                    )                                                                             AS RemainingDay,
                    cand.ReviewType,
                    DATEDIFF(DAY,
                        CAST(appl.AppointmentDateTime AS date),
                        CAST(GETDATE() AS date)
                    )                                                                             AS DaysSinceLastAppraisal,
                    CAST(ROUND(
                        {center}.STDistance(COALESCE(cand.GeoPoint, appl.GeoPoint)) / 1000.0,
                        3
                    ) AS float)                                                                   AS DistanceKm,
                    COALESCE(cand.Latitude,  appl.Latitude)                                      AS Latitude,
                    COALESCE(cand.Longitude, appl.Longitude)                                     AS Longitude
                FROM AppraisalCoords appl
                FULL OUTER JOIN CandidateCoords cand
                    ON cand.SurveyNumber = appl.AppraisalNumber
                WHERE
                    (appl.AppraisalId IS NOT NULL OR cand.Id IS NOT NULL)
                    AND (cand.Id      IS NULL OR cand.Id        <> @SelfCandidateId)
                    AND (appl.AppraisalId IS NULL OR appl.AppraisalId <> @SelfAppraisalId)
                    -- In-flight check sourced from the Appraisal table (not RequestDetails):
                    -- exclude rows whose prior appraisal is already referenced by ANY non-terminal
                    -- Appraisal (Status != Completed/Cancelled), regardless of AppraisalType
                    -- (ReAppraisal, Progressive/CI, Appeal) — we block any duplicate in-flight work.
                    AND NOT EXISTS (
                        SELECT 1
                        FROM appraisal.Appraisals openA
                        WHERE openA.PrevAppraisalId = COALESCE(
                                  appl.AppraisalId,
                                  (SELECT a2.Id FROM appraisal.Appraisals a2
                                   WHERE a2.AppraisalNumber = cand.SurveyNumber)
                              )
                          AND openA.Status NOT IN ('Completed', 'Cancelled')
                          AND openA.IsDeleted = 0
                    )
                ORDER BY DistanceKm ASC
                """;

            var nearby = await conn.QueryAsync<NearbyReappraisalCandidate>(
                nearbySql,
                new
                {
                    RadiusM = radiusM,
                    SelfCandidateId = query.Id,
                    SelfAppraisalId = selfAppraisalId
                });

            detail.NearbyGroupCandidates = nearby.ToList();
        }

        return new GetReappraisalCandidateByIdResult(detail);
    }

    /// <summary>
    /// Resolves the in-system AppraisalId for the main candidate's SurveyNumber.
    /// Returns Guid.Empty when no match (safe to compare — no appraisal has that ID).
    /// </summary>
    private static async Task<Guid> ResolveSelfAppraisalIdAsync(
        System.Data.IDbConnection conn,
        string surveyNumber,
        CancellationToken _)
    {
        const string sql = """
            SELECT TOP 1 a.Id
            FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber = @SurveyNumber
            """;
        var id = await conn.QueryFirstOrDefaultAsync<Guid?>(sql, new { SurveyNumber = surveyNumber });
        return id ?? Guid.Empty;
    }

    /// <summary>
    /// Produces a safe inline geography::Point expression (same as HistorySearchQueryHandler).
    /// Lat/Lon come from our own database — clamped as an extra safety guard.
    /// </summary>
    private static string BuildGeoPoint(decimal lat, decimal lon)
    {
        var safeLat = Math.Clamp((double)lat, -90.0, 90.0);
        var safeLon = Math.Clamp((double)lon, -180.0, 180.0);
        return FormattableString.Invariant(
            $"geography::Point({safeLat:F6}, {safeLon:F6}, 4326)");
    }
}
