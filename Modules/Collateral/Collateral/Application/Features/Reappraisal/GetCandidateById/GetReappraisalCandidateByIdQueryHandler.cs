using Dapper;

namespace Collateral.Application.Features.Reappraisal.GetCandidateById;

/// <summary>
/// Read-side handler for the Reappraisal Candidate detail page.
/// Returns the candidate's full detail plus a list of nearby appraisals/candidates
/// within <c>RadiusKm</c> (default 1 km) for the "Group Appraisal" selection table.
/// Reads from collateral.vw_ReappraisalCandidates and collateral.ReappraisalCandidates.
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
                c.ReviewDate,
                c.RemainingDay,
                c.OldAppraisalReportNumber,
                c.NormalizedSurveyNumber,
                c.PriorAppraisalSource,
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
                c.OpenAppraisalGroupTag,
                c.OpenRequestId,
                c.OpenRequestNumber,
                c.FirstSeenFileDate,
                c.LastSeenFileDate,
                c.IsBlockUnit
            FROM collateral.vw_ReappraisalCandidates c
            WHERE c.Id = @Id
            """;

        var detail = await conn.QueryFirstOrDefaultAsync<ReappraisalCandidateDetail>(
            detailSql, new { query.Id });

        if (detail is null) return null;

        // Resolve the main candidate's own in-system AppraisalId.
        var selfAppraisalId = await ResolveSelfAppraisalIdAsync(
            conn, detail.NormalizedSurveyNumber, cancellationToken);
        detail.AppraisalId = selfAppraisalId == Guid.Empty ? null : selfAppraisalId;

        // ── Block-project unit: the project and the matched unit ─────────────────
        if (detail.IsBlockUnit)
        {
            const string unitSql = """
                SELECT cu.ProjectAppraisalId, a.AppraisalNumber AS ProjectAppraisalNumber, p.ProjectType,
                       p.ProjectName, va.ValuationDate AS ProjectValuationDate, cu.MatchedUnits, cu.MatchedBy,
                       u.TowerName, u.Floor, u.RoomNumber, u.CondoRegistrationNumber, u.UsableArea,
                       u.ModelType, u.HouseNumber, u.PlotNumber, u.LandArea,
                       pr.TotalAppraisalValueRounded AS UnitPrice
                FROM collateral.vw_ReappraisalCandidateUnits cu
                JOIN appraisal.Projects p ON p.Id = cu.ProjectId
                JOIN appraisal.Appraisals a ON a.Id = cu.ProjectAppraisalId
                LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
                LEFT JOIN appraisal.ProjectUnits u ON u.Id = cu.ProjectUnitId
                LEFT JOIN appraisal.ProjectUnitPrices pr ON pr.ProjectUnitId = u.Id
                WHERE cu.CandidateId = @Id
                """;
            detail.Unit = await conn.QueryFirstOrDefaultAsync<BlockUnitInfo>(unitSql, new { query.Id });
        }

        // ── Processed book: the reappraisal it produced ─────────────────────────
        // Newest non-cancelled first — the same rule as the processed tab and RCAS002.
        if (detail.Status == "Consumed")
        {
            const string newAppraisalSql = """
                SELECT TOP 1
                    rb.AppraisalId     AS NewAppraisalId,
                    rb.AppraisalNumber AS NewAppraisalNumber,
                    rb.Status          AS NewAppraisalStatus,
                    rb.GroupTag        AS NewAppraisalGroupTag,
                    r.RequestedAt      AS NewAppraisalSubmittedAt,
                    rb.CompletedAt     AS NewAppraisalCompletedAt
                FROM appraisal.vw_ReappraisalsByBook rb
                LEFT JOIN request.Requests r ON r.Id = rb.RequestId
                WHERE rb.BookNumber = @Book
                  -- A block-project unit: only the reappraisal raised for this collateral.
                  AND (@IsBlockUnit = 0 OR rb.CollateralId = @CollateralId)
                ORDER BY CASE WHEN rb.Status = 'Cancelled' THEN 1 ELSE 0 END, rb.CreatedAt DESC
                """;
            var na = await conn.QueryFirstOrDefaultAsync<NewAppraisalRow>(
                newAppraisalSql, new { Book = detail.NormalizedSurveyNumber, detail.IsBlockUnit, detail.CollateralId });
            if (na is not null)
            {
                detail.NewAppraisalId = na.NewAppraisalId;
                detail.NewAppraisalNumber = na.NewAppraisalNumber;
                detail.NewAppraisalStatus = na.NewAppraisalStatus;
                detail.NewAppraisalGroupTag = na.NewAppraisalGroupTag;
                detail.NewAppraisalSubmittedAt = na.NewAppraisalSubmittedAt;
                detail.NewAppraisalCompletedAt = na.NewAppraisalCompletedAt;
            }
        }

        // ── Nearby group candidates query ────────────────────────────────────────
        if (detail.Latitude.HasValue && detail.Longitude.HasValue)
        {
            var center = BuildGeoPoint(detail.Latitude.Value, detail.Longitude.Value);
            var radiusM = (double)(query.RadiusKm * 1000);

            var nearbySql = $"""
                WITH LatestFile AS (
                    -- Same rule as the list (collateral.vw_ReappraisalLatestFile).
                    SELECT FileDate FROM collateral.vw_ReappraisalLatestFile
                ),
                AppraisalCoords AS (
                    SELECT
                        a.Id              AS AppraisalId,
                        a.AppraisalNumber,
                        al.CustomerName,
                        -- Appraisal date = ValuationAnalyses.ValuationDate, appointment as fallback.
                        -- ValuationDate leads because an off-system external engagement has no
                        -- Appointment row at all.
                        -- Same rule as the list (vw_ReappraisalCandidates.last_appr): CompletedAt last, for a
                        -- migrated appraisal with neither a valuation row nor an appointment.
                        COALESCE(va.ValuationDate, al.AppointmentDateTime, a.CompletedAt) AS AppraisalDate,
                        CAST(d.Latitude   AS decimal(10,7)) AS Latitude,
                        CAST(d.Longitude  AS decimal(10,7)) AS Longitude,
                        geography::Point(
                            CAST(d.Latitude  AS float),
                            CAST(d.Longitude AS float),
                            4326
                        ) AS GeoPoint
                    FROM appraisal.Appraisals a
                    JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
                    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
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
                      AND a.BankingSegment = 'IBG'
                      AND a.Status = 'Completed'
                ),
                CandidateCoords AS (
                    SELECT rc.Id, rc.SurveyNumber, rc.CollateralId, rc.IsBlockUnit,
                           rc.NormalizedSurveyNumber AS BookNumber, rc.GeoPoint,
                           rc.CifName, rc.ReviewDate, rc.ReviewType, rc.CurrentValue, rc.ValuationDate,
                           rc.Latitude, rc.Longitude,
                           -- one row per book: a book listed under several collateral is one reappraisal
                           -- (each collateral of a block project is its own unit, so its own row). The
                           -- soonest-due copy stands for it, whole — each collateral carries its own
                           -- ReviewDate. Which collateral it is only matters for a block unit (its own row
                           -- anyway): any other book's request is matched and consumed by book.
                           ROW_NUMBER() OVER (PARTITION BY rc.NormalizedSurveyNumber,
                                                           CASE WHEN rc.IsBlockUnit = 1 THEN rc.CollateralId END
                                              ORDER BY rc.ReviewDate, rc.Id) AS BookRn
                    FROM collateral.ReappraisalCandidates rc
                    WHERE rc.Status = 'Pending'
                      -- Same rule as the list: only books on AS400's latest file. Hides books that
                      -- dropped off, and the stale Pending copies left from before books were deduplicated.
                      -- ("Under review" is checked for every nearby row in the WHERE below.)
                      AND COALESCE(rc.LastSeenFileDate, rc.SourceFileDate) = (SELECT FileDate FROM LatestFile)
                      AND rc.GeoPoint IS NOT NULL
                      AND {center}.STDistance(rc.GeoPoint) <= @RadiusM
                      AND rc.IBGRetail = 'IBG'
                )
                SELECT
                    appl.AppraisalId,
                    cand.Id                                                                     AS CandidateId,
                    CASE WHEN cand.Id IS NOT NULL THEN 'Candidate' ELSE 'InSystem' END          AS Source,
                    COALESCE(cand.SurveyNumber,   appl.AppraisalNumber)                         AS OldAppraisalReportNumber,
                    COALESCE(cand.CifName,         appl.CustomerName)                           AS CustomerName,
                    cand.CurrentValue,
                    -- With no CAS appraisal in range (appl): the listing for a 99A book, then the file's date.
                    -- A CAS book whose own appraisal is outside AppraisalCoords also lands here, so it can
                    -- differ from the list's CAS date — kept cheap rather than a CAS lookup per row.
                    dt.AppraisalDate                                                              AS AppraisalDate,
                    -- Due = the review date AS400 sent; an in-system appraisal not on the file has none.
                    cand.ReviewDate                                                               AS ReviewDate,
                    DATEDIFF(DAY, CAST(GETDATE() AS date), cand.ReviewDate)                       AS RemainingDay,
                    cand.ReviewType,
                    DATEDIFF(DAY, dt.AppraisalDate, CAST(GETDATE() AS date))                      AS DaysSinceLastAppraisal,
                    CAST(ROUND(
                        {center}.STDistance(COALESCE(cand.GeoPoint, appl.GeoPoint)) / 1000.0,
                        3
                    ) AS float)                                                                   AS DistanceKm,
                    COALESCE(cand.Latitude,  appl.Latitude)                                      AS Latitude,
                    COALESCE(cand.Longitude, appl.Longitude)                                     AS Longitude,
                    -- Under review: an open reappraisal of the book, or a request for it still waiting.
                    -- Listed (greyed out, not selectable) so staff see why a nearby book is not offered.
                    CAST(CASE WHEN EXISTS (
                                  SELECT 1 FROM appraisal.vw_ReappraisalsByBook rb
                                  WHERE rb.BookNumber = COALESCE(appl.AppraisalNumber, cand.BookNumber)
                                    AND rb.Status NOT IN ('Completed', 'Cancelled')
                                    AND (ISNULL(cand.IsBlockUnit, 0) = 0 OR rb.CollateralId = cand.CollateralId))
                              OR EXISTS (
                                  SELECT 1 FROM request.vw_WaitingReappraisalRequests w
                                  WHERE w.BookNumber = COALESCE(appl.AppraisalNumber, cand.BookNumber)
                                    AND (ISNULL(cand.IsBlockUnit, 0) = 0 OR w.CollateralId = cand.CollateralId))
                         THEN 1 ELSE 0 END AS bit)                                                AS IsInProgress
                FROM AppraisalCoords appl
                FULL OUTER JOIN (SELECT * FROM CandidateCoords WHERE BookRn = 1) cand
                    ON cand.BookNumber = appl.AppraisalNumber
                -- Only when CAS supplied no date (appl): the listing is a bank-supplied table, probed per book at most.
                OUTER APPLY (SELECT TOP 1 lv.ValuationDate
                             FROM appraisal.vw_LegacyBookLatestValuation lv
                             WHERE appl.AppraisalId IS NULL AND lv.BookNumber = cand.BookNumber) lg
                CROSS APPLY (SELECT CAST(COALESCE(appl.AppraisalDate, lg.ValuationDate, cand.ValuationDate) AS date) AS AppraisalDate) dt
                WHERE
                    (appl.AppraisalId IS NOT NULL OR cand.Id IS NOT NULL)
                    AND (cand.Id      IS NULL OR cand.Id        <> @SelfCandidateId)
                    AND (appl.AppraisalId IS NULL OR appl.AppraisalId <> @SelfAppraisalId)
                    -- Not this book again under another collateral — unless it is another unit of the same
                    -- block project, which is reviewed on its own.
                    AND (@SelfBook IS NULL OR cand.IsBlockUnit = 1
                         OR COALESCE(appl.AppraisalNumber, cand.BookNumber) <> @SelfBook)
                    -- Not a book already reappraised to completion: it is superseded — its successor is the
                    -- book to review next, and initiating it would fork the chain.
                    AND NOT EXISTS (
                        SELECT 1 FROM appraisal.vw_ReappraisalsByBook rb
                        WHERE rb.BookNumber = COALESCE(appl.AppraisalNumber, cand.BookNumber)
                          AND rb.Status = 'Completed'
                          AND (ISNULL(cand.IsBlockUnit, 0) = 0 OR rb.CollateralId = cand.CollateralId)
                    )
                ORDER BY DistanceKm ASC
                """;

            var nearby = await conn.QueryAsync<NearbyReappraisalCandidate>(
                nearbySql,
                new
                {
                    RadiusM = radiusM,
                    SelfCandidateId = query.Id,
                    SelfAppraisalId = selfAppraisalId,
                    SelfBook = detail.NormalizedSurveyNumber
                });

            detail.NearbyGroupCandidates = nearby.ToList();
        }

        return new GetReappraisalCandidateByIdResult(detail);
    }

    private static async Task<Guid> ResolveSelfAppraisalIdAsync(
        System.Data.IDbConnection conn,
        string surveyNumber,
        CancellationToken _)
    {
        const string sql = """
            SELECT TOP 1 a.Id
            FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber = @SurveyNumber
              AND a.IsDeleted = 0
            ORDER BY a.Id  -- same pick as Initiate (MIN(Id)) and the candidate view
            """;
        var id = await conn.QueryFirstOrDefaultAsync<Guid?>(sql, new { SurveyNumber = surveyNumber });
        return id ?? Guid.Empty;
    }

    private sealed class NewAppraisalRow
    {
        public Guid? NewAppraisalId { get; set; }
        public string? NewAppraisalNumber { get; set; }
        public string? NewAppraisalStatus { get; set; }
        public string? NewAppraisalGroupTag { get; set; }
        public DateTime? NewAppraisalSubmittedAt { get; set; }
        public DateTime? NewAppraisalCompletedAt { get; set; }
    }

    private static string BuildGeoPoint(decimal lat, decimal lon)
    {
        var safeLat = Math.Clamp((double)lat, -90.0, 90.0);
        var safeLon = Math.Clamp((double)lon, -180.0, 180.0);
        return FormattableString.Invariant(
            $"geography::Point({safeLat:F6}, {safeLon:F6}, 4326)");
    }
}
