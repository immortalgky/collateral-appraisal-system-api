CREATE
OR ALTER VIEW common.vw_MonitoringPendingTasks
AS
-- Resolution chain mirrors workflow.vw_TaskMonitor:
--   PendingTask.CorrelationId -> request.Requests.Id -> appraisal.Appraisals.RequestId
--   DocumentFollowup fallback: PendingTask.WorkflowInstanceId
--     -> workflow.DocumentFollowups.FollowupWorkflowInstanceId
--     -> DocumentFollowup.RequestId -> Appraisal
--
-- C4 fix: both Appraisal join and AppraisalAssignment join use OUTER APPLY TOP 1
--         to guarantee at most one row per PendingTask (a Request may have multiple
--         Appraisals from CI/Appeal flows; an Appraisal may have multiple active
--         AppraisalAssignments momentarily during reassignment).
--
-- M1 fix: PIC shows the user's full name when AssignedType='1' (person-assigned),
--         falling back to the raw AssignedTo value (pool name) for AssignedType='2'.
--         Mirrors vw_TaskMonitor lines 22-24.
--
-- M4 note: TRY_CAST on aa.AssigneeCompanyId is intentional — that column is stored as
--          nvarchar (string) in appraisal.AppraisalAssignments while auth.Companies.Id
--          is uniqueidentifier. TRY_CAST silently returns NULL for malformed values.
--
-- MonitoringType discriminator:
--   'External' when the current AppraisalAssignment is AssignmentType=External,
--               Status NOT IN ('Pending','Cancelled'), SubmittedAt IS NULL
--   'Internal' otherwise
--
-- OLA columns use wall-clock DATEDIFF(HOUR, ...) per FSD §2.6.8 semantics.
--
-- Purpose: returns the raw purpose code (e.g. 'REQUEST_FOR_CREDIT_LIMIT').
--          FE resolves description via useParameterDescription('AppraisalPurpose', code).
--
-- PropertyType: returns the raw code (e.g. 'L', 'B', 'U') from request.RequestProperties.
--               Sourced from the request because it's always present from request creation —
--               CollateralEngagements only exist after later appraisal-lifecycle steps and are
--               null for pending tasks. FE resolves via useParameterDescription('PropertyType', code).
SELECT
    pt.Id                                                                       AS PendingTaskId,
    appl.Id                                                                     AS AppraisalId,
    appl.AppraisalNumber                                                        AS AppraisalNumber,
    OUTER_C.Name                                                                AS CustomerName,
    -- TaskType returns the activityName prefix only. ApprovalActivity stores TaskName as
    -- "{activityName}:{username}" (one row per committee member) — stripping the suffix
    -- here makes the filter `TaskType IN ('PendingApproval')` match those rows. Regular
    -- TaskActivity rows have no colon and are unaffected by the CASE.
    CASE
        WHEN CHARINDEX(':', pt.TaskName) > 0
            THEN LEFT(pt.TaskName, CHARINDEX(':', pt.TaskName) - 1)
        ELSE pt.TaskName
    END                                                                         AS TaskType,
    pt.TaskDescription                                                          AS TaskDescription,
    r.Purpose                                                                   AS Purpose,
    rp.PropertyType                                                             AS PropertyType,
    pt.SlaStatus                                                                AS SlaStatus,
    appl.RequestedAt                                                            AS RequestedDate,
    pt.AssignedAt                                                               AS AssignedDate,
    -- M1: PIC display name resolution.
    --     type='1' (person)  → "First Last" from auth.AspNetUsers; NULL if names are empty so
    --                          the FE can render the username as the primary line.
    --     type='2' (pool)    → group's Description from auth.Groups (joined on Name = AssignedTo);
    --                          falls back to the raw AssignedTo when no matching group exists.
    CASE
        WHEN pt.AssignedType = '1'
        THEN NULLIF(CONCAT(u.FirstName, ' ', u.LastName), ' ')
        ELSE COALESCE(NULLIF(g.Description, ''), pt.AssignedTo)
    END                                                                         AS PIC,
    pt.AssignedTo                                                               AS AssignedTo,
    pt.AssignedType                                                             AS AssignedType,
    pt.Movement                                                                 AS Movement,
    DATEDIFF(HOUR, pt.AssignedAt, pt.DueAt)                                     AS OlaTargetHours,
    DATEDIFF(HOUR, pt.AssignedAt, GETDATE())                                    AS OlaActualHours,
    DATEDIFF(HOUR, pt.AssignedAt, GETDATE())
        - DATEDIFF(HOUR, pt.AssignedAt, pt.DueAt)                               AS OlaVarianceHours,
    COALESCE(appl.Priority, r.Priority)                                         AS Priority,
    pt.ActivityId                                                               AS ActivityId,
    pt.AssigneeCompanyId                                                        AS AssigneeCompanyId,
    aa.AssigneeCompanyId                                                        AS AppraisalCompanyId,
    comp.Name                                                                   AS AppraisalCompanyName,
    CASE
        WHEN aa.AssignmentType = 'External'
         AND aa.AssignmentStatus NOT IN ('Pending', 'Cancelled')
         AND aa.SubmittedAt IS NULL
        THEN 'External'
        ELSE 'Internal'
    END                                                                         AS MonitoringType
FROM workflow.PendingTasks pt
    -- DocumentFollowup fallback: resolves followup-workflow tasks back to the originating request
    OUTER APPLY (
        SELECT TOP 1 df2.Id, df2.RequestId
        FROM workflow.DocumentFollowups df2
        WHERE df2.FollowupWorkflowInstanceId = pt.WorkflowInstanceId
    ) df
    LEFT JOIN request.Requests r
           ON r.Id = COALESCE(df.RequestId, pt.CorrelationId)
    -- C4 fix: OUTER APPLY TOP 1 prevents fan-out when a Request has multiple Appraisals
    OUTER APPLY (
        SELECT TOP 1 a2.Id, a2.AppraisalNumber,
                     a2.RequestedAt, a2.Priority
        FROM appraisal.Appraisals a2
        WHERE a2.RequestId = r.Id
        ORDER BY a2.RequestedAt DESC, a2.Id DESC
    ) appl
    -- Customer name (first row per request)
    OUTER APPLY (
        SELECT TOP 1 Name
        FROM request.RequestCustomers
        WHERE RequestId = r.Id
    ) OUTER_C
    -- PropertyType: first RequestProperty for the originating request. TOP 1 prevents fan-out
    --               for requests with multiple properties; the first property is the primary.
    OUTER APPLY (
        SELECT TOP 1 PropertyType
        FROM request.RequestProperties
        WHERE RequestId = r.Id
    ) rp
    -- C4 fix: OUTER APPLY TOP 1 prevents fan-out from multiple active AppraisalAssignments
    --         during reassignment windows. Most-recent active External first, then any active.
    OUTER APPLY (
        SELECT TOP 1 aa2.AssignmentType, aa2.AssignmentStatus, aa2.SubmittedAt,
                     aa2.AssigneeCompanyId
        FROM appraisal.AppraisalAssignments aa2
        WHERE aa2.AppraisalId = appl.Id
          AND aa2.SubmittedAt IS NULL
          AND aa2.AssignmentStatus NOT IN ('Pending', 'Cancelled')
        ORDER BY
            CASE WHEN aa2.AssignmentType = 'External' THEN 0 ELSE 1 END,
            aa2.AssignedAt DESC, aa2.Id DESC
    ) aa
    -- M1: join user for display name (person-assigned tasks only)
    LEFT JOIN auth.AspNetUsers u
           ON u.NormalizedUserName = UPPER(pt.AssignedTo)
          AND pt.AssignedType = '1'
    -- M1: join group for display name (pool tasks only). PoolAssigneeSelector stores the group
    --     name string in pt.AssignedTo, so we match by Name. If the group is renamed in auth.Groups
    --     the view reflects the new name; if the group was deleted (IsDeleted=1) the join misses
    --     and the CASE above falls back to the raw stored value.
    LEFT JOIN auth.Groups g
           ON g.Name = pt.AssignedTo
          AND g.IsDeleted = 0
          AND pt.AssignedType = '2'
    -- Company name for the assigned company
    -- M4 note: TRY_CAST is intentional — aa.AssigneeCompanyId is nvarchar, Companies.Id is uniqueidentifier
    LEFT JOIN auth.Companies comp
           ON comp.Id = TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)
WHERE pt.TaskStatus IN ('Assigned', 'InProgress')
  -- Skip pending tasks whose Appraisal record hasn't been generated yet
  AND appl.Id IS NOT NULL;
