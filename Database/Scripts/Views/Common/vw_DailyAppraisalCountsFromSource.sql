CREATE
OR ALTER
VIEW common.vw_DailyAppraisalCountsFromSource AS
/*
 * Canonical daily appraisal counts derived directly from source tables.
 * Used by POST /dashboard/reconcile-appraisal-counts to correct drift in
 * the event-sourced common.DailyAppraisalCounts read-model.
 *
 * CreatedCount  — appraisals created on each calendar day.
 * CompletedCount — appraisals whose Status = 'Completed' and UpdatedAt falls on
 *                  that day.  appraisal.Appraisals has no dedicated CompletedAt
 *                  column; UpdatedAt is the best available proxy.
 *
 * TODO: add a CompletedAt column to appraisal.Appraisals and update this view
 *       to use it for an exact completion date.
 */
SELECT
    d.Date,
    ISNULL(c.CreatedCount, 0)   AS CreatedCount,
    ISNULL(x.CompletedCount, 0) AS CompletedCount
FROM (
    -- Union of all distinct dates from both created and completed events
    SELECT CAST(a.CreatedAt AS DATE) AS Date FROM appraisal.Appraisals a WHERE a.IsDeleted = 0
    UNION
    SELECT CAST(a.UpdatedAt AS DATE) AS Date FROM appraisal.Appraisals a WHERE a.IsDeleted = 0 AND a.Status = 'Completed' AND a.UpdatedAt IS NOT NULL
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
    -- Approximate completion date via UpdatedAt where Status = 'Completed'.
    SELECT
        CAST(a.UpdatedAt AS DATE) AS Date,
        COUNT(*)                   AS CompletedCount
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
      AND a.Status = 'Completed'
      AND a.UpdatedAt IS NOT NULL
    GROUP BY CAST(a.UpdatedAt AS DATE)
) x ON x.Date = d.Date;
