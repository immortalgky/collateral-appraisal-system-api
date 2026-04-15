CREATE OR ALTER VIEW workflow.vw_SlaTaskList
AS
SELECT
    pt.Id AS TaskId,
    pt.CorrelationId,
    CAST(pt.TaskName AS nvarchar(100)) AS TaskName,
    pt.TaskDescription,
    pt.AssignedTo,
    pt.AssignedType,
    pt.AssignedAt,
    pt.DueAt,
    pt.SlaStatus,
    pt.SlaBreachedAt,
    pt.WorkingBy,
    DATEDIFF(HOUR, pt.AssignedAt, GETUTCDATE()) AS ElapsedHours,
    CASE
        WHEN pt.DueAt IS NOT NULL THEN DATEDIFF(HOUR, GETUTCDATE(), pt.DueAt)
        ELSE NULL
        END AS RemainingHours,
    a.Id AS AppraisalId,
    a.AppraisalNumber,
    a.AppraisalType,
    r.Id AS RequestId,
    r.Purpose AS RequestPurpose,
    wi.Id AS WorkflowInstanceId,
    wi.WorkflowDueAt,
    wi.WorkflowSlaStatus,
    -- Company from latest appraisal assignment
    aa.AssigneeCompanyId,
    -- Loan type from workflow variables (stored as JSON)
    NULL AS LoanType -- Can be derived from workflow variables if needed
FROM workflow.PendingTasks pt
         LEFT JOIN appraisal.Appraisals a ON a.RequestId = pt.CorrelationId
         LEFT JOIN request.Requests r ON r.Id = pt.CorrelationId
         LEFT JOIN workflow.WorkflowInstances wi
                   ON wi.CorrelationId = CAST(pt.CorrelationId AS nvarchar(450))
                       AND wi.Status = 'Running'
         LEFT JOIN (
    SELECT
        AppraisalId,
        AssigneeCompanyId,
        ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY CreatedAt DESC) AS rn
    FROM appraisal.AppraisalAssignments
    WHERE AssignmentStatus NOT IN ('Rejected', 'Cancelled')
) aa ON aa.AppraisalId = a.Id AND aa.rn = 1
WHERE pt.DueAt IS NOT NULL