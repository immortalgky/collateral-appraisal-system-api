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

    -- Composite score — only computed when an evaluation row exists.
    -- Ratings are nullable (Pending rows may have partial selections); treat NULL as 0
    -- so a partial draft still produces a usable partial score.
    -- Weights MUST stay in sync with AppraisalEvaluation.Criterion{N}Weight constants
    -- (Modules/Appraisal/.../Domain/Evaluations/AppraisalEvaluation.cs).
    CASE
        WHEN e.Id IS NOT NULL
            THEN CAST(
                (0.40 * ISNULL(e.Criteria1Rating, 0))
                    + (0.30 * ISNULL(e.Criteria2Rating, 0))
                    + (0.10 * ISNULL(e.Criteria3Rating, 0))
                    + (0.10 * ISNULL(e.Criteria4Rating, 0))
                    + (0.10 * ISNULL(e.Criteria5Rating, 0))
            AS DECIMAL(5, 2))
        ELSE NULL
        END                                 AS TotalScore

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
            aa.SubmittedAt,
            ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC) AS rn
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AssignmentType = 'External'
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) ext
ON ext.AppraisalId = a.Id AND ext.rn = 1

    -- Final appraised value
    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id

    -- Appraiser company (resolved from the current external assignment)
    LEFT JOIN auth.Companies comp
    ON comp.Id = TRY_CAST(ext.AssigneeCompanyId AS uniqueidentifier)

    -- Evaluation (may not exist)
    LEFT JOIN appraisal.AppraisalEvaluations e ON e.AppraisalId = a.Id

-- Worklist: every appraisal whose current external assignment has been submitted.
-- Evaluations in any state (Pending / Draft / Completed) all surface here; the
-- caller filters by EvaluationStatus to split active work from history.
WHERE a.IsDeleted = 0
  AND ext.SubmittedAt IS NOT NULL
