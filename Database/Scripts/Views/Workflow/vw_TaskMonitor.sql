CREATE
OR ALTER
VIEW workflow.vw_TaskMonitor
AS
-- Supervisor-facing task monitor view.
-- Scoped to person-assigned tasks (AssignedType = '1') in Assigned or InProgress state.
-- Joins with auth schema to provide assignee identity and their primary group for monitoring.
SELECT
    pt.Id                                                              AS TaskId,
    a.Id                                                               AS AppraisalId,
    pt.WorkflowInstanceId,
    pt.ActivityId,
    -- TaskName is the workflow's human-readable task label (e.g. "Internal Appraisal Check").
    -- It is also used as the activity display name since there is no separate activity-name source.
    pt.TaskName                                                        AS TaskName,
    pt.TaskDescription                                                 AS TaskDescription,
    pt.TaskName                                                        AS ActivityName,
    a.AppraisalNumber,
    r.Id                                                               AS RequestId,
    r.RequestNumber,
    OUTER_C.Name                                                       AS CustomerName,
    pt.AssignedTo                                                      AS AssignedTo,
    COALESCE(NULLIF(CONCAT(u.FirstName, ' ', u.LastName), ' '), pt.AssignedTo)
                                                                       AS AssignedToDisplayName,
    -- Primary group this user belongs to (first alphabetically among non-deleted groups)
    OUTER_G.Id                                                         AS GroupId,
    OUTER_G.Name                                                       AS GroupName,
    -- Request-level columns for the drill-down task table
    COALESCE(purp.Description, r.Purpose)                              AS Purpose,
    rd.FacilityLimit                                                   AS FacilityLimit,
    rd.PrevAppraisalNumber                                             AS PrevAppraisalNumber,
    pt.TaskStatus                                                      AS TaskStatus,
    a.Status                                                           AS AppraisalStatus,
    pt.AssignedAt                                                      AS AssignedAt,
    pt.DueAt,
    pt.SlaStatus,
    -- ElapsedHours / RemainingHours are computed in C# (GetMonitoredTasksQueryHandler) using
    -- IBusinessTimeCalculator so they exclude weekends, holidays and lunch. They are NOT derived
    -- here: a SQL DATEDIFF would count calendar hours (nights/weekends included).
    -- AssignedAt (elapsed start) and DueAt (remaining end) are already exposed above.
    pt.WorkingBy,
    pt.LockedAt
FROM workflow.PendingTasks pt
    -- Resolve appraisal via CorrelationId (regular tasks) or DocumentFollowup
    OUTER APPLY (SELECT TOP 1 Id, RequestId
                 FROM workflow.DocumentFollowups
                 WHERE FollowupWorkflowInstanceId = pt.WorkflowInstanceId) df
    LEFT JOIN appraisal.Appraisals a ON a.Id =
        (SELECT TOP 1 a2.Id FROM appraisal.Appraisals a2
         WHERE a2.RequestId = COALESCE(df.RequestId, pt.CorrelationId))
    LEFT JOIN request.Requests r ON r.Id = COALESCE(df.RequestId, pt.CorrelationId)
    LEFT JOIN request.RequestDetails rd ON rd.RequestId = r.Id
    -- Resolve Purpose code (e.g. "01") to English label (e.g. "Request for credit limit")
    OUTER APPLY (SELECT TOP 1 Description FROM parameter.Parameters
                 WHERE [Group] = 'AppraisalPurpose' AND [Language] = 'EN' AND [Code] = r.Purpose) purp
    OUTER APPLY (SELECT TOP 1 Name FROM request.RequestCustomers WHERE RequestId = r.Id) OUTER_C
    -- Assignee identity
    LEFT JOIN auth.AspNetUsers u ON u.NormalizedUserName = UPPER(pt.AssignedTo)
    -- Primary group (first non-deleted group the user belongs to, alphabetical by name)
    OUTER APPLY (
        SELECT TOP 1 g.Id, g.Name
        FROM auth.Groups g
        INNER JOIN auth.GroupUsers gu ON gu.GroupId = g.Id
        INNER JOIN auth.AspNetUsers gu_u ON gu_u.Id = gu.UserId
            AND gu_u.NormalizedUserName = UPPER(pt.AssignedTo)
        WHERE g.IsDeleted = 0
        ORDER BY g.Name
    ) OUTER_G
-- Only person-assigned tasks in active states
WHERE pt.AssignedType = '1'
  AND pt.TaskStatus IN ('Assigned', 'InProgress')
