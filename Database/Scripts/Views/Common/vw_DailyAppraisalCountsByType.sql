CREATE
OR ALTER
VIEW common.vw_DailyAppraisalCountsByType AS
/*
 * Canonical daily appraisal counts, broken down by AppraisalType and BankingSegment,
 * derived directly from source tables.
 *
 * Backs GET /dashboard/appraisal-counts:
 *   - Overview mode sums CreatedCount / CompletedCount over all types/segments per period.
 *   - By-type mode groups additionally by AppraisalType (one series per type).
 *   - An optional BankingSegment filter narrows to a single segment.
 *
 * BankingSegment is normalized with ISNULL(..., 'IBG') so NULL rows count as IBG,
 * matching the convention used elsewhere in the Appraisal module.
 *
 * CreatedCount   — appraisals of that type/segment created on each calendar day.
 * CompletedCount — appraisals of that type/segment whose CompletedAt falls on that day.
 */
SELECT
    d.Date,
    d.AppraisalType,
    d.BankingSegment,
    ISNULL(c.CreatedCount, 0)   AS CreatedCount,
    ISNULL(x.CompletedCount, 0) AS CompletedCount
FROM (
    -- Union of all distinct (date, type, segment) tuples from both created and completed events
    SELECT CAST(a.CreatedAt AS DATE) AS Date, a.AppraisalType AS AppraisalType,
           ISNULL(a.BankingSegment, 'IBG') AS BankingSegment
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
    UNION
    SELECT CAST(a.CompletedAt AS DATE) AS Date, a.AppraisalType AS AppraisalType,
           ISNULL(a.BankingSegment, 'IBG') AS BankingSegment
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0 AND a.CompletedAt IS NOT NULL
) d
LEFT JOIN (
    SELECT
        CAST(a.CreatedAt AS DATE)       AS Date,
        a.AppraisalType                 AS AppraisalType,
        ISNULL(a.BankingSegment, 'IBG') AS BankingSegment,
        COUNT(*)                        AS CreatedCount
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
    GROUP BY CAST(a.CreatedAt AS DATE), a.AppraisalType, ISNULL(a.BankingSegment, 'IBG')
) c ON c.Date = d.Date AND c.AppraisalType = d.AppraisalType AND c.BankingSegment = d.BankingSegment
LEFT JOIN (
    SELECT
        CAST(a.CompletedAt AS DATE)     AS Date,
        a.AppraisalType                 AS AppraisalType,
        ISNULL(a.BankingSegment, 'IBG') AS BankingSegment,
        COUNT(*)                        AS CompletedCount
    FROM appraisal.Appraisals a
    WHERE a.IsDeleted = 0
      AND a.CompletedAt IS NOT NULL
    GROUP BY CAST(a.CompletedAt AS DATE), a.AppraisalType, ISNULL(a.BankingSegment, 'IBG')
) x ON x.Date = d.Date AND x.AppraisalType = d.AppraisalType AND x.BankingSegment = d.BankingSegment;
