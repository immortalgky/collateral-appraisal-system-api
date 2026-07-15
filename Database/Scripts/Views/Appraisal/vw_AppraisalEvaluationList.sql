CREATE
OR ALTER
VIEW appraisal.vw_AppraisalEvaluationList AS
SELECT
    -- Appraisal core
    a.Id                                    AS AppraisalId,
    a.AppraisalNumber,
    a.Status                                AS AppraisalStatus,

    -- Customer name from request
    c.Name                                  AS CustomerName,

    -- Report received date: appraiser handoff timestamp (first InProgress -> UnderReview)
    ext.SubmittedAt                         AS ReportReceivedDate,

    -- External appraiser info (most recent non-cancelled, non-rejected External assignment)
    ext.ExternalAppraiserName,
    ext.AssigneeCompanyId,
    comp.Name                               AS AppraiserCompanyName,

    -- Final appraised value from valuation analysis
    va.AppraisedValue                       AS AppraisalValue,

    -- Evaluation fields (NULL when no evaluation exists yet)
    e.Id                                    AS EvaluationId,
    COALESCE(e.EvaluationStatus, 'Pending') AS EvaluationStatus,

    -- Composite score — only produced when an evaluation row exists.
    -- Completed evaluations carry a frozen snapshot (e.TotalScore) stamped at completion
    -- time, so later edits to EvaluationCriteriaConfig weights do NOT rewrite historical
    -- scores (or the company averages derived from them).
    -- Pending rows have no snapshot yet, so they are scored live from the current config
    -- weights — ratings are nullable (partial drafts), treat NULL as 0 for a partial preview.
    -- Weights are loaded from appraisal.EvaluationCriteriaConfigs keyed by BankingSegment.
    -- NULL BankingSegment defaults to 'IBG' weights (ISNULL fallback in OUTER APPLY).
    CASE
        WHEN e.Id IS NULL THEN NULL
        WHEN e.TotalScore IS NOT NULL THEN e.TotalScore
        ELSE CAST(
                (ISNULL(w.W1, 0) * ISNULL(e.Criteria1Rating, 0))
                    + (ISNULL(w.W2, 0) * ISNULL(e.Criteria2Rating, 0))
                    + (ISNULL(w.W3, 0) * ISNULL(e.Criteria3Rating, 0))
                    + (ISNULL(w.W4, 0) * ISNULL(e.Criteria4Rating, 0))
                    + (ISNULL(w.W5, 0) * ISNULL(e.Criteria5Rating, 0))
            AS DECIMAL(5, 2))
        END                                 AS TotalScore,

    -- Internal followup staff: the bank-side user attached to the current external
    -- assignment (AppraisalAssignment.InternalAppraiserId, stored as a username).
    -- Name is resolved from auth.AspNetUsers, mirroring vw_AppraisalEvaluationHeader.
    ext.InternalAppraiserId                 AS InternalFollowupStaffId,
    NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))), '')
                                            AS InternalFollowupStaffName

FROM appraisal.Appraisals a

     -- Customer name
    OUTER APPLY (
        SELECT TOP 1 Name
        FROM request.RequestCustomers
        WHERE RequestId = a.RequestId
    ) c

    -- Most recent External assignment that is not Rejected / Cancelled
    LEFT JOIN (
        SELECT
            aa.AppraisalId,
            aa.ExternalAppraiserName,
            aa.AssigneeCompanyId,
            aa.InternalAppraiserId,
            aa.SubmittedAt,
            ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC) AS rn
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AssignmentType = 'External'
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) ext
ON ext.AppraisalId = a.Id AND ext.rn = 1

    -- Internal followup staff display name (person-assigned; username -> AspNetUsers)
    LEFT JOIN auth.AspNetUsers u
    ON u.NormalizedUserName = UPPER(ext.InternalAppraiserId)

    -- Final appraised value
    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id

    -- Appraiser company (resolved from the current external assignment)
    LEFT JOIN auth.Companies comp
    ON comp.Id = TRY_CAST(ext.AssigneeCompanyId AS uniqueidentifier)

    -- Evaluation (may not exist)
    LEFT JOIN appraisal.AppraisalEvaluations e ON e.AppraisalId = a.Id

    -- Config-driven weights: pivot the 5 slots for this appraisal's BankingSegment.
    -- ISNULL(a.BankingSegment, 'IBG') defaults NULL-segment appraisals to IBG weights.
    -- UPPER() comparison is case-insensitive.
    OUTER APPLY (
        SELECT
            MAX(CASE WHEN cfg.CriteriaSlot = 1 THEN cfg.Weight END) AS W1,
            MAX(CASE WHEN cfg.CriteriaSlot = 2 THEN cfg.Weight END) AS W2,
            MAX(CASE WHEN cfg.CriteriaSlot = 3 THEN cfg.Weight END) AS W3,
            MAX(CASE WHEN cfg.CriteriaSlot = 4 THEN cfg.Weight END) AS W4,
            MAX(CASE WHEN cfg.CriteriaSlot = 5 THEN cfg.Weight END) AS W5
        FROM appraisal.EvaluationCriteriaConfigs cfg
        WHERE UPPER(cfg.BankingSegment) = UPPER(ISNULL(a.BankingSegment, 'IBG'))
    ) w

-- Worklist: every appraisal whose current external assignment has been submitted.
-- Evaluations in any state (Pending / Completed) all surface here; the
-- caller filters by EvaluationStatus to split active work from history.
WHERE a.IsDeleted = 0
  AND ext.SubmittedAt IS NOT NULL
