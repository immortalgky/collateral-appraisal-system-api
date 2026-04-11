CREATE OR ALTER VIEW workflow.vw_SlaComplianceSummary
AS
SELECT
    CAST(ct.TaskName AS nvarchar(100)) AS TaskName,
    COUNT(*) AS TotalTasks,
    SUM(CASE WHEN ct.SlaStatus = 'ON_TIME' THEN 1 ELSE 0 END) AS OnTimeCount,
    SUM(CASE WHEN ct.SlaStatus = 'AT_RISK' THEN 1 ELSE 0 END) AS AtRiskCount,
    SUM(CASE WHEN ct.SlaStatus = 'BREACHED' THEN 1 ELSE 0 END) AS BreachedCount,
    SUM(CASE WHEN ct.SlaStatus IS NULL THEN 1 ELSE 0 END) AS NoSlaCount,
    AVG(DATEDIFF(HOUR, ct.AssignedAt, ct.CompletedAt)) AS AvgCompletionHours,
    MIN(DATEDIFF(HOUR, ct.AssignedAt, ct.CompletedAt)) AS MinCompletionHours,
    MAX(DATEDIFF(HOUR, ct.AssignedAt, ct.CompletedAt)) AS MaxCompletionHours,
    -- Company dimension
    aa.AssigneeCompanyId,
    -- Time dimension
    DATEPART(MONTH, ct.CompletedAt) AS CompletionMonth,
    DATEPART(YEAR, ct.CompletedAt) AS CompletionYear,
    -- User dimension
    ct.AssignedTo
FROM workflow.CompletedTasks ct
         LEFT JOIN appraisal.Appraisals a ON a.RequestId = ct.CorrelationId
         LEFT JOIN (
    SELECT
        AppraisalId,
        AssigneeCompanyId,
        ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY CreatedAt DESC) AS rn
    FROM appraisal.AppraisalAssignments
    WHERE AssignmentStatus NOT IN ('REJECTED', 'CANCELLED')
) aa ON aa.AppraisalId = a.Id AND aa.rn = 1
WHERE ct.DueAt IS NOT NULL
GROUP BY
    CAST(ct.TaskName AS nvarchar(100)),
    aa.AssigneeCompanyId,
    DATEPART(MONTH, ct.CompletedAt),
    DATEPART(YEAR, ct.CompletedAt),
    ct.AssignedTo
