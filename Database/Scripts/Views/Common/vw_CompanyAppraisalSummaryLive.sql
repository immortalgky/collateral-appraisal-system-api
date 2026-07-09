CREATE
OR ALTER
VIEW common.vw_CompanyAppraisalSummaryLive AS
/*
 * One row per appraisal (its latest non-cancelled external assignment) classified into
 * exactly one of three mutually-exclusive buckets. Read live by
 * GET /dashboard/company-appraisal-summary so Completed + InProgress + Overdue = Assigned
 * for every company, with no double-counting.
 *
 * Grain      — distinct appraisal = latest AppraisalAssignment with AssigneeCompanyId,
 *              same canonical population as vw_CompanyAppraisalSummariesFromSource.
 * IsCompleted  — the external company delivered (SubmittedAt set = first hand-off).
 * IsOverdue    — not yet delivered and past the frozen SLA due date (matches the
 *                'Breached' rule in appraisal.vw_AssignmentList). SLADueDate NULL ⇒ never overdue.
 * IsInProgress — not yet delivered and either on-time or with no SLA policy.
 *
 * GETDATE() keeps overdue live/point-in-time (local kind, matches SLADueDate).
 */
WITH latest_assignment AS (
    SELECT
        a.Id                                                AS AppraisalId,
        a.CreatedAt,
        TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)  AS CompanyId,
        comp.Name                                           AS CompanyName,
        aa.SubmittedAt,
        aa.SLADueDate
    FROM appraisal.Appraisals a
    INNER JOIN (
        SELECT AppraisalId, AssigneeCompanyId, SubmittedAt, SLADueDate,
               ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY CreatedAt DESC) AS rn
        FROM appraisal.AppraisalAssignments
        WHERE AssigneeCompanyId IS NOT NULL
          AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) aa ON aa.AppraisalId = a.Id AND aa.rn = 1
    LEFT JOIN auth.Companies comp
        ON comp.Id = TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)
    WHERE a.IsDeleted = 0
)
SELECT
    AppraisalId,
    CompanyId,
    CompanyName,
    CreatedAt,
    CASE WHEN SubmittedAt IS NOT NULL THEN 1 ELSE 0 END AS IsCompleted,
    CASE WHEN SubmittedAt IS NULL AND SLADueDate IS NOT NULL
              AND SLADueDate < GETDATE() THEN 1 ELSE 0 END AS IsOverdue,
    CASE WHEN SubmittedAt IS NULL AND (SLADueDate IS NULL OR SLADueDate >= GETDATE())
              THEN 1 ELSE 0 END AS IsInProgress
FROM latest_assignment;
