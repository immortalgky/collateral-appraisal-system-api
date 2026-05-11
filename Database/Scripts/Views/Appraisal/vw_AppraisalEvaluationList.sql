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

    -- Report received date: use CompletedAt as the closest proxy
    a.CompletedAt                           AS ReportReceivedDate,

    -- External appraiser info (most recent non-cancelled, non-rejected External assignment)
    ext.ExternalAppraiserName,
    ext.AssigneeCompanyId,

    -- Final appraised value from valuation analysis
    va.AppraisedValue                       AS AppraisalValue,

    -- Evaluation fields (NULL when no evaluation exists yet)
    e.Id                                    AS EvaluationId,
    COALESCE(e.EvaluationStatus, 'Pending') AS EvaluationStatus,

    -- Composite score — only computed when an evaluation row exists
    CASE
        WHEN e.Id IS NOT NULL
            THEN CAST(
                     (0.40 * e.Criteria1Rating)
                     + (0.30 * e.Criteria2Rating)
                     + (0.10 * e.Criteria3Rating)
                     + (0.10 * e.Criteria4Rating)
                     + (0.10 * e.Criteria5Rating)
                     AS DECIMAL(5, 2))
        ELSE NULL
    END                                     AS TotalScore

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
            ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC) AS rn
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AssignmentType = 'External'
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) ext ON ext.AppraisalId = a.Id AND ext.rn = 1

    -- Final appraised value
    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id

    -- Evaluation (may not exist)
    LEFT JOIN appraisal.AppraisalEvaluations e ON e.AppraisalId = a.Id

-- Only show appraisals that have (or had) an External assignment
WHERE a.IsDeleted = 0
  AND EXISTS (
      SELECT 1
      FROM appraisal.AppraisalAssignments aa2
      WHERE aa2.AppraisalId = a.Id
        AND aa2.AssignmentType = 'External'
  )
