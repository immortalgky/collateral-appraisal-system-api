CREATE
OR ALTER VIEW common.vw_CompanyOverview AS
-- One row per auth.Companies entry, enriched with:
--   AverageRating    — AVG of TotalScore from completed evaluations
--   EvaluationCount  — count of completed evaluations with a score
--   ActiveAssignments — count of pending-external monitoring tasks
--
-- All metrics are LEFT JOIN based so every company gets a row (zeros when absent).
-- TRY_CAST bridges nvarchar AssigneeCompanyId / AppraisalCompanyId → uniqueidentifier.
SELECT
    c.Id                                AS CompanyId,
    ISNULL(ev.AverageRating, 0)         AS AverageRating,
    ISNULL(ev.EvaluationCount, 0)       AS EvaluationCount,
    ISNULL(aa.ActiveAssignments, 0)     AS ActiveAssignments

FROM auth.Companies c

    -- Completed evaluations aggregate per company
    LEFT JOIN (
        SELECT
            TRY_CAST(el.AssigneeCompanyId AS uniqueidentifier) AS CompanyId,
            AVG(el.TotalScore)                                 AS AverageRating,
            COUNT(*)                                           AS EvaluationCount
        FROM appraisal.vw_AppraisalEvaluationList el
        WHERE el.EvaluationStatus = 'Completed'
          AND el.TotalScore IS NOT NULL
          AND TRY_CAST(el.AssigneeCompanyId AS uniqueidentifier) IS NOT NULL
        GROUP BY TRY_CAST(el.AssigneeCompanyId AS uniqueidentifier)
    ) ev ON ev.CompanyId = c.Id

    -- Active external assignments aggregate per company
    LEFT JOIN (
        SELECT
            TRY_CAST(mt.AppraisalCompanyId AS uniqueidentifier) AS CompanyId,
            COUNT(*)                                            AS ActiveAssignments
        FROM common.vw_MonitoringPendingTasks mt
        WHERE mt.MonitoringType = 'External'
          AND TRY_CAST(mt.AppraisalCompanyId AS uniqueidentifier) IS NOT NULL
        GROUP BY TRY_CAST(mt.AppraisalCompanyId AS uniqueidentifier)
    ) aa ON aa.CompanyId = c.Id
