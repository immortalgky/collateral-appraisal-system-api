CREATE OR ALTER VIEW workflow.vw_WorkflowSlaSummary
AS
SELECT
    wi.Id AS WorkflowInstanceId,
    wi.Name AS WorkflowName,
    wi.CorrelationId,
    wi.Status,
    wi.StartedOn,
    wi.CompletedOn,
    wi.WorkflowDueAt,
    wi.WorkflowSlaStatus,
    wi.CurrentActivityId,
    wi.CurrentAssignee,
    wi.StartedBy,
    -- StartedOn / CompletedOn / WorkflowDueAt are stored in application-local time, so compare
    -- against local GETDATE() (not GETUTCDATE()) — matching the other SLA/task list views.
    DATEDIFF(HOUR, wi.StartedOn, COALESCE(wi.CompletedOn, GETDATE())) AS ElapsedHours,
    CASE
        WHEN wi.WorkflowDueAt IS NOT NULL THEN DATEDIFF(HOUR, GETDATE(), wi.WorkflowDueAt)
        ELSE NULL
        END AS RemainingHours,
    -- Count of breached activities for this workflow
    (
        SELECT COUNT(*)
        FROM workflow.PendingTasks pt
        WHERE pt.CorrelationId = CAST(wi.CorrelationId AS uniqueidentifier)
          AND pt.SlaStatus = 'Breached'
    ) AS BreachedActivityCount,
    -- Count of at-risk activities
    (
        SELECT COUNT(*)
        FROM workflow.PendingTasks pt
        WHERE pt.CorrelationId = CAST(wi.CorrelationId AS uniqueidentifier)
          AND pt.SlaStatus = 'AtRisk'
    ) AS AtRiskActivityCount
FROM workflow.WorkflowInstances wi
WHERE wi.WorkflowDueAt IS NOT NULL