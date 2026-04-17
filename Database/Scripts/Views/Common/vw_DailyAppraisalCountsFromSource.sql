CREATE
OR ALTER
VIEW common.vw_DailyAppraisalCountsFromSource AS
/*
 * Canonical daily appraisal counts derived directly from source tables.
 * Used by POST /dashboard/reconcile-appraisal-counts to correct drift in
 * the event-sourced common.DailyAppraisalCounts read-model.
 *
 * CreatedCount   — appraisals created on each calendar day.
 * CompletedCount — appraisals whose CompletedAt falls on that day.
 */
SELECT
    d.Date,
    ISNULL(c.CreatedCount, 0)   AS CreatedCount,
    ISNULL(x.CompletedCount, 0) AS CompletedCount
FROM (
    -- Union of all distinct dates from both created and completed events
    SELECT CAST(a.CreatedAt AS DATE) AS Date FROM appraisal.Appraisals a WHERE a.IsDeleted = 0
    UNION
    SELECT CAST(a.CompletedAt AS DATE) AS Date FROM appraisal.Appraisals a WHERE a.IsDeleted = 0 AND a.CompletedAt IS NOT NULL
) d
LEFT JOIN (
    SELECT
        CAST(a.CreatedAt AS DATE) AS Date,
        COUNT(*)                   AS CreatedCount
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
    GROUP BY CAST(a.CreatedAt AS DATE)
) c ON c.Date = d.Date
LEFT JOIN (
    SELECT
        CAST(a.CompletedAt AS DATE) AS Date,
        COUNT(*)                    AS CompletedCount
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
      AND a.CompletedAt IS NOT NULL
    GROUP BY CAST(a.CompletedAt AS DATE)
) x ON x.Date = d.Date;
