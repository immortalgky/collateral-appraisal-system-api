CREATE
OR ALTER
VIEW common.vw_CompanyAppraisalSummariesFromSource AS
/*
 * Canonical per-(Company, Day) appraisal counts derived directly from source tables.
 * Used by POST /dashboard/reconcile-company-appraisal-summaries to correct drift in
 * the event-sourced common.CompanyAppraisalSummaries read-model.
 *
 * AssignedCount   — appraisals whose latest non-cancelled external assignment lands with this company,
 *                   counted on the appraisal's CreatedAt day.
 * CompletedCount  — those same appraisals counted on their CompletedAt day.
 */
WITH latest_assignment AS (
    SELECT
        a.Id                                                AS AppraisalId,
        a.CreatedAt,
        a.CompletedAt,
        TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)  AS CompanyId,
        comp.Name                                           AS CompanyName
    FROM appraisal.Appraisals a
    INNER JOIN (
        SELECT AppraisalId, AssigneeCompanyId,
               ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY CreatedAt DESC) AS rn
        FROM appraisal.AppraisalAssignments
        WHERE AssigneeCompanyId IS NOT NULL
          AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) aa ON aa.AppraisalId = a.Id AND aa.rn = 1
    LEFT JOIN auth.Companies comp
        ON comp.Id = TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)
    WHERE a.IsDeleted = 0
),
events AS (
    SELECT CompanyId, CompanyName, CAST(CreatedAt AS DATE) AS [Date], 1 AS AssignedCount, 0 AS CompletedCount
    FROM latest_assignment
    UNION ALL
    SELECT CompanyId, CompanyName, CAST(CompletedAt AS DATE) AS [Date], 0, 1
    FROM latest_assignment
    WHERE CompletedAt IS NOT NULL
)
SELECT
    CompanyId,
    [Date],
    MAX(CompanyName)        AS CompanyName,
    SUM(AssignedCount)      AS AssignedCount,
    SUM(CompletedCount)     AS CompletedCount
FROM events
GROUP BY CompanyId, [Date];
