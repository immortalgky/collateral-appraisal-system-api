CREATE OR ALTER VIEW common.vw_MonitoringCommitteeApprovalTasks
AS
SELECT
    pt.AssignedTo,
    v.AppraisalId,
    v.AppraisalNumber,
    v.CustomerName,
    v.ApprovalTier,
    v.MeetingNumber,
    v.MeetingDate
FROM workflow.PendingTasks pt
INNER JOIN workflow.WorkflowInstances wi
    ON wi.Id = pt.WorkflowInstanceId
INNER JOIN common.vw_MonitoringPendingApprovals v
    ON v.AppraisalId = TRY_CAST(JSON_VALUE(wi.Variables, '$.appraisalId') AS UNIQUEIDENTIFIER)
LEFT JOIN workflow.Meetings m
    ON m.Id = v.MeetingId
WHERE pt.TaskName LIKE 'PendingApproval%';