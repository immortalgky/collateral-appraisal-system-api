CREATE OR ALTER PROCEDURE [workflow].[sp_GetTaskGroupCounts]
    -- Access scope (mirrors workflow.sp_GetTaskList)
    @AssignedType     CHAR(1),               -- '1' = my (direct), '2' = pool (group/team)
    @Assignees        NVARCHAR(MAX),         -- CSV of candidate assignee ids (username/group/group:Team_<guid>)
    @CompanyGate      TINYINT      = 0,       -- 0 = no gate, 1 = (NULL OR = @CallerCompanyId), 2 = (NULL only)
    @CallerCompanyId  UNIQUEIDENTIFIER = NULL,
    -- Filters (all optional; NULL = ignore) -- SAME surface/semantics as sp_GetTaskList
    @Status               NVARCHAR(50)  = NULL,
    @Priority             NVARCHAR(50)  = NULL,
    @TaskName             NVARCHAR(100) = NULL,
    @Search               NVARCHAR(200) = NULL,  -- already LIKE-escaped by caller; matched with ESCAPE '\'
    @AppraisalNumber      NVARCHAR(50)  = NULL,
    @CustomerName         NVARCHAR(200) = NULL,
    @TaskStatus           NVARCHAR(50)  = NULL,
    @TaskType             NVARCHAR(100) = NULL,
    @DateFrom             DATETIME2     = NULL,
    @DateTo               DATETIME2     = NULL,
    @AppointmentDateFrom  DATETIME2     = NULL,
    @AppointmentDateTo    DATETIME2     = NULL,
    @RequestedAtFrom      DATETIME2     = NULL,
    @RequestedAtTo        DATETIME2     = NULL,
    @SlaStatus            NVARCHAR(50)  = NULL,  -- 'NoSla' sentinel = IS NULL; else equality
    @ActivityId           NVARCHAR(100) = NULL,
    @Purpose              NVARCHAR(200) = NULL,
    @TaskStatusBucket     NVARCHAR(20)  = NULL,  -- 'NotStarted' | 'InProgress' | 'Overdue' (mutually exclusive)
    -- Grouping
    @GroupBy              NVARCHAR(20)           -- required: 'status' | 'priority' | 'purpose' | 'activity' | 'slaStatus'
AS
BEGIN
    SET NOCOUNT ON;

    ----------------------------------------------------------------------------
    -- Same enrichment/heavy-join gating as sp_GetTaskList's @NeedEnrich/@NeedHeavy,
    -- plus: grouping by 'purpose'/'priority' always needs the Requests/Appraisals
    -- join (those columns don't exist on workflow.PendingTasks). 'activity',
    -- 'status' and 'slaStatus' group on base PendingTasks columns, so they only
    -- escalate when one of the OTHER active filters forces enrichment.
    ----------------------------------------------------------------------------
    DECLARE @NeedEnrich BIT = CASE WHEN
           @Status IS NOT NULL OR @Priority IS NOT NULL OR @AppraisalNumber IS NOT NULL
        OR @CustomerName IS NOT NULL OR @Search IS NOT NULL OR @Purpose IS NOT NULL
        OR @AppointmentDateFrom IS NOT NULL OR @AppointmentDateTo IS NOT NULL
        OR @RequestedAtFrom IS NOT NULL OR @RequestedAtTo IS NOT NULL
        OR @GroupBy IN ('purpose', 'priority')
      THEN 1 ELSE 0 END;

    DECLARE @NeedHeavy BIT = CASE WHEN
           @AppointmentDateFrom IS NOT NULL OR @AppointmentDateTo IS NOT NULL
      THEN 1 ELSE 0 END;

    CREATE TABLE #f_cust   (RequestId UNIQUEIDENTIFIER PRIMARY KEY);  -- @CustomerName matches
    CREATE TABLE #f_appr   (RequestId UNIQUEIDENTIFIER PRIMARY KEY);  -- @AppraisalNumber matches
    CREATE TABLE #f_search (RequestId UNIQUEIDENTIFIER PRIMARY KEY);  -- @Search (name OR number) matches

    IF @CustomerName IS NOT NULL
        INSERT INTO #f_cust (RequestId)
        SELECT DISTINCT RequestId FROM request.RequestCustomers
        WHERE RequestId IS NOT NULL AND Name LIKE @CustomerName ESCAPE '\'
        OPTION (RECOMPILE, MAXDOP 1);

    IF @AppraisalNumber IS NOT NULL
        INSERT INTO #f_appr (RequestId)
        SELECT DISTINCT RequestId FROM appraisal.Appraisals
        WHERE RequestId IS NOT NULL AND AppraisalNumber LIKE @AppraisalNumber ESCAPE '\'
        OPTION (RECOMPILE, MAXDOP 1);

    IF @Search IS NOT NULL
        INSERT INTO #f_search (RequestId)
        SELECT RequestId FROM request.RequestCustomers
          WHERE RequestId IS NOT NULL AND Name LIKE @Search ESCAPE '\'
        UNION
        SELECT RequestId FROM appraisal.Appraisals
          WHERE RequestId IS NOT NULL AND AppraisalNumber LIKE @Search ESCAPE '\'
        OPTION (RECOMPILE, MAXDOP 1);

    IF @NeedEnrich = 0
    BEGIN
        ----------------------------------------------------------------------
        -- BASE-ONLY: 'activity' | 'status' | 'slaStatus' grouping with no
        -- enriched filter active. Groups straight off workflow.PendingTasks
        -- (mirrors sp_GetTaskList's cheap browse-path predicate set).
        ----------------------------------------------------------------------
        ;WITH gc AS (
            SELECT
                CASE @GroupBy
                    WHEN 'activity'  THEN ActivityId
                    WHEN 'status'    THEN CASE WHEN SlaStatus = 'Breached' THEN 'Overdue'
                                                WHEN TaskStatus = 'Assigned' THEN 'NotStarted'
                                                WHEN TaskStatus IN ('InProgress', 'Completing') THEN 'InProgress'
                                                ELSE 'NotStarted' END
                    WHEN 'slaStatus' THEN CASE WHEN SlaStatus = 'Breached' THEN 'Breached'
                                                WHEN SlaStatus = 'AtRisk' THEN 'AtRisk'
                                                WHEN SlaStatus IN ('OnTime', 'OnTrack') THEN 'OnTime'
                                                ELSE 'NoSla' END
                END AS GroupValue
            FROM workflow.PendingTasks
            WHERE AssignedType = @AssignedType
              AND AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ','))
              AND ( @CompanyGate = 0
                     OR (@CompanyGate = 1 AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @CallerCompanyId))
                     OR (@CompanyGate = 2 AND AssigneeCompanyId IS NULL) )
              AND (@TaskName   IS NULL OR TaskName   = @TaskName)
              AND (@TaskType   IS NULL OR TaskName   = @TaskType)
              AND (@ActivityId IS NULL OR ActivityId = @ActivityId)
              AND (@TaskStatus IS NULL OR TaskStatus = @TaskStatus)
              AND (@SlaStatus IS NULL
                     OR (@SlaStatus = 'NoSla' AND SlaStatus IS NULL)
                     OR (@SlaStatus <> 'NoSla' AND SlaStatus = @SlaStatus))
              AND (@TaskStatusBucket IS NULL
                     OR (@TaskStatusBucket = 'NotStarted' AND TaskStatus = 'Assigned' AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))
                     OR (@TaskStatusBucket = 'InProgress' AND TaskStatus IN ('InProgress', 'Completing') AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))
                     OR (@TaskStatusBucket = 'Overdue' AND SlaStatus = 'Breached'))
              AND (@DateFrom   IS NULL OR AssignedAt >= @DateFrom)
              AND (@DateTo     IS NULL OR AssignedAt <  DATEADD(day, 1, @DateTo))
        )
        SELECT GroupValue AS [Value], COUNT(*) AS [Count]
        FROM gc
        WHERE GroupValue IS NOT NULL
        GROUP BY GroupValue
        OPTION (RECOMPILE);
    END
    ELSE IF @NeedHeavy = 1
    BEGIN
        ----------------------------------------------------------------------
        -- FULL ENRICHED: only reachable when @AppointmentDateFrom/To is also
        -- active (AppointmentDateTime lives behind the AppraisalAssignments/
        -- Appointments joins). Mirrors sp_GetTaskList's full-enriched path;
        -- exposes ALL 5 group columns so any @GroupBy value works here too.
        ----------------------------------------------------------------------
        ;WITH gc_filtered AS (
            SELECT Id, CorrelationId, AssignedTo, AssignedType, AssigneeCompanyId,
                   AssignedAt, DueAt, Movement, TaskName, TaskStatus, SlaStatus, ActivityId
            FROM   workflow.PendingTasks
            WHERE  AssignedType = @AssignedType
              AND  AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ','))
              AND  ( @CompanyGate = 0
                     OR (@CompanyGate = 1 AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @CallerCompanyId))
                     OR (@CompanyGate = 2 AND AssigneeCompanyId IS NULL) )
              AND  (@TaskName   IS NULL OR TaskName   = @TaskName)
              AND  (@TaskType   IS NULL OR TaskName   = @TaskType)
              AND  (@ActivityId IS NULL OR ActivityId = @ActivityId)
              AND  (@TaskStatus IS NULL OR TaskStatus = @TaskStatus)
              AND  (@SlaStatus IS NULL
                     OR (@SlaStatus = 'NoSla' AND SlaStatus IS NULL)
                     OR (@SlaStatus <> 'NoSla' AND SlaStatus = @SlaStatus))
              AND  (@TaskStatusBucket IS NULL
                     OR (@TaskStatusBucket = 'NotStarted' AND TaskStatus = 'Assigned' AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))
                     OR (@TaskStatusBucket = 'InProgress' AND TaskStatus IN ('InProgress', 'Completing') AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))
                     OR (@TaskStatusBucket = 'Overdue' AND SlaStatus = 'Breached'))
              AND  (@DateFrom   IS NULL OR AssignedAt >= @DateFrom)
              AND  (@DateTo     IS NULL OR AssignedAt <  DATEADD(day, 1, @DateTo))
        ),
        gc_resolved AS (
            -- Branch 1: QUOTATION (CorrelationId = QuotationRequests.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   qra.AppraisalId AS RAppraisalId, CAST(NULL AS uniqueidentifier) AS RRequestIdOverride
            FROM   gc_filtered pt
            JOIN   appraisal.QuotationRequests qr ON qr.Id = pt.CorrelationId
            OUTER APPLY (SELECT TOP 1 AppraisalId FROM appraisal.QuotationRequestAppraisals
                         WHERE QuotationRequestId = qr.Id ORDER BY AppraisalId) qra
            LEFT JOIN appraisal.Appraisals aq ON aq.Id = qra.AppraisalId
            WHERE  (@CustomerName    IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_search))
            UNION ALL
            -- Branch 2: FEE-APPROVAL (CorrelationId = FeeAppointmentApprovals.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   faa.AppraisalId, CAST(NULL AS uniqueidentifier)
            FROM   gc_filtered pt
            JOIN   workflow.FeeAppointmentApprovals faa ON faa.Id = pt.CorrelationId
            LEFT JOIN appraisal.Appraisals af ON af.Id = faa.AppraisalId
            WHERE  (@CustomerName    IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_search))
            UNION ALL
            -- Branch 3: DOCUMENT-FOLLOWUP (CorrelationId = DocumentFollowups.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   df.AppraisalId, df.RequestId
            FROM   gc_filtered pt
            JOIN   workflow.DocumentFollowups df ON df.Id = pt.CorrelationId
            WHERE  (@CustomerName    IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_search))
            UNION ALL
            -- Branch 4: NORMAL (CorrelationId = Requests.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   (SELECT TOP 1 a3.Id FROM appraisal.Appraisals a3 WHERE a3.RequestId = pt.CorrelationId ORDER BY a3.Id),
                   pt.CorrelationId
            FROM   gc_filtered pt
            JOIN   request.Requests r4 ON r4.Id = pt.CorrelationId
            WHERE  (@CustomerName    IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_search))
        ),
        gc_enriched AS (
            SELECT
                gc_resolved.TaskStatus, gc_resolved.SlaStatus, gc_resolved.ActivityId,
                a.Status                                   AS AStatus,
                COALESCE(a.Priority, r.Priority)           AS Priority,
                r.Purpose,
                ap.AppointmentDateTime
            FROM gc_resolved
                LEFT JOIN appraisal.Appraisals a ON a.Id = gc_resolved.RAppraisalId
                LEFT JOIN request.Requests     r ON r.Id = COALESCE(gc_resolved.RRequestIdOverride, a.RequestId)
                OUTER APPLY (SELECT TOP 1 Id, AssignmentType, AssigneeUserId, InternalAppraiserId, AssigneeCompanyId
                             FROM appraisal.AppraisalAssignments
                             WHERE AppraisalId = a.Id AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                             ORDER BY AssignedAt DESC, CreatedAt DESC, Id DESC) AA
                OUTER APPLY (SELECT TOP 1 AppointmentDateTime FROM appraisal.Appointments
                             WHERE AssignmentId = AA.Id AND Status != 'Cancelled') ap
        ),
        gc AS (
            SELECT
                CASE @GroupBy
                    WHEN 'activity'  THEN ActivityId
                    WHEN 'priority'  THEN COALESCE(Priority, 'Normal')
                    WHEN 'purpose'   THEN Purpose
                    WHEN 'status'    THEN CASE WHEN SlaStatus = 'Breached' THEN 'Overdue'
                                                WHEN TaskStatus = 'Assigned' THEN 'NotStarted'
                                                WHEN TaskStatus IN ('InProgress', 'Completing') THEN 'InProgress'
                                                ELSE 'NotStarted' END
                    WHEN 'slaStatus' THEN CASE WHEN SlaStatus = 'Breached' THEN 'Breached'
                                                WHEN SlaStatus = 'AtRisk' THEN 'AtRisk'
                                                WHEN SlaStatus IN ('OnTime', 'OnTrack') THEN 'OnTime'
                                                ELSE 'NoSla' END
                END AS GroupValue
            FROM gc_enriched
            WHERE (@Status   IS NULL OR AStatus = @Status)
              AND (@Priority IS NULL OR Priority = @Priority)
              -- @AppraisalNumber / @CustomerName / @Search are applied per-branch in gc_resolved (#f_cust/#f_appr/#f_search pushdown).
              AND (@AppointmentDateFrom IS NULL OR AppointmentDateTime >= @AppointmentDateFrom)
              AND (@AppointmentDateTo   IS NULL OR AppointmentDateTime <  DATEADD(day, 1, @AppointmentDateTo))
        )
        SELECT GroupValue AS [Value], COUNT(*) AS [Count]
        FROM gc
        WHERE GroupValue IS NOT NULL
        GROUP BY GroupValue
        OPTION (RECOMPILE, MAXDOP 1);
    END
    ELSE
    BEGIN
        ----------------------------------------------------------------------
        -- LIGHT ENRICHED: 'purpose'/'priority' grouping (or another filter
        -- forcing enrichment) without needing AppointmentDateTime. Mirrors
        -- sp_GetTaskList's light+name-pushdown path.
        ----------------------------------------------------------------------
        ;WITH gc_filtered AS (
            SELECT Id, CorrelationId, AssignedTo, AssignedType, AssigneeCompanyId,
                   AssignedAt, DueAt, Movement, TaskName, TaskStatus, SlaStatus, ActivityId
            FROM   workflow.PendingTasks
            WHERE  AssignedType = @AssignedType
              AND  AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ','))
              AND  ( @CompanyGate = 0
                     OR (@CompanyGate = 1 AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @CallerCompanyId))
                     OR (@CompanyGate = 2 AND AssigneeCompanyId IS NULL) )
              AND  (@TaskName   IS NULL OR TaskName   = @TaskName)
              AND  (@TaskType   IS NULL OR TaskName   = @TaskType)
              AND  (@ActivityId IS NULL OR ActivityId = @ActivityId)
              AND  (@TaskStatus IS NULL OR TaskStatus = @TaskStatus)
              AND  (@SlaStatus IS NULL
                     OR (@SlaStatus = 'NoSla' AND SlaStatus IS NULL)
                     OR (@SlaStatus <> 'NoSla' AND SlaStatus = @SlaStatus))
              AND  (@TaskStatusBucket IS NULL
                     OR (@TaskStatusBucket = 'NotStarted' AND TaskStatus = 'Assigned' AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))
                     OR (@TaskStatusBucket = 'InProgress' AND TaskStatus IN ('InProgress', 'Completing') AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))
                     OR (@TaskStatusBucket = 'Overdue' AND SlaStatus = 'Breached'))
              AND  (@DateFrom   IS NULL OR AssignedAt >= @DateFrom)
              AND  (@DateTo     IS NULL OR AssignedAt <  DATEADD(day, 1, @DateTo))
        ),
        gc_resolved AS (
            -- Branch 1: QUOTATION (CorrelationId = QuotationRequests.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   qra.AppraisalId AS RAppraisalId, CAST(NULL AS uniqueidentifier) AS RRequestIdOverride
            FROM   gc_filtered pt
            JOIN   appraisal.QuotationRequests qr ON qr.Id = pt.CorrelationId
            OUTER APPLY (SELECT TOP 1 AppraisalId FROM appraisal.QuotationRequestAppraisals
                         WHERE QuotationRequestId = qr.Id ORDER BY AppraisalId) qra
            LEFT JOIN appraisal.Appraisals aq ON aq.Id = qra.AppraisalId
            WHERE  (@CustomerName    IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_search))
            UNION ALL
            -- Branch 2: FEE-APPROVAL (CorrelationId = FeeAppointmentApprovals.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   faa.AppraisalId, CAST(NULL AS uniqueidentifier)
            FROM   gc_filtered pt
            JOIN   workflow.FeeAppointmentApprovals faa ON faa.Id = pt.CorrelationId
            LEFT JOIN appraisal.Appraisals af ON af.Id = faa.AppraisalId
            WHERE  (@CustomerName    IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_search))
            UNION ALL
            -- Branch 3: DOCUMENT-FOLLOWUP (CorrelationId = DocumentFollowups.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   df.AppraisalId, df.RequestId
            FROM   gc_filtered pt
            JOIN   workflow.DocumentFollowups df ON df.Id = pt.CorrelationId
            WHERE  (@CustomerName    IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_search))
            UNION ALL
            -- Branch 4: NORMAL (CorrelationId = Requests.Id)
            SELECT pt.Id, pt.TaskStatus, pt.SlaStatus, pt.ActivityId,
                   (SELECT TOP 1 a3.Id FROM appraisal.Appraisals a3 WHERE a3.RequestId = pt.CorrelationId ORDER BY a3.Id),
                   pt.CorrelationId
            FROM   gc_filtered pt
            JOIN   request.Requests r4 ON r4.Id = pt.CorrelationId
            WHERE  (@CustomerName    IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_cust))
              AND  (@AppraisalNumber IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_appr))
              AND  (@Search          IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_search))
        ),
        gc_light AS (
            SELECT
                gc_resolved.TaskStatus, gc_resolved.SlaStatus, gc_resolved.ActivityId,
                a.Status                                   AS AStatus,
                COALESCE(a.Priority, r.Priority)           AS Priority,
                r.Purpose,
                COALESCE(a.RequestedAt, r.RequestedAt)     AS RequestReceivedDate
            FROM gc_resolved
                LEFT JOIN appraisal.Appraisals a ON a.Id = gc_resolved.RAppraisalId
                LEFT JOIN request.Requests     r ON r.Id = COALESCE(gc_resolved.RRequestIdOverride, a.RequestId)
            -- NO RequestCustomers/RequestProperties/AppraisalAssignments/Appointments/Companies here
        ),
        gc AS (
            SELECT
                CASE @GroupBy
                    WHEN 'activity'  THEN ActivityId
                    WHEN 'priority'  THEN COALESCE(Priority, 'Normal')
                    WHEN 'purpose'   THEN Purpose
                    WHEN 'status'    THEN CASE WHEN SlaStatus = 'Breached' THEN 'Overdue'
                                                WHEN TaskStatus = 'Assigned' THEN 'NotStarted'
                                                WHEN TaskStatus IN ('InProgress', 'Completing') THEN 'InProgress'
                                                ELSE 'NotStarted' END
                    WHEN 'slaStatus' THEN CASE WHEN SlaStatus = 'Breached' THEN 'Breached'
                                                WHEN SlaStatus = 'AtRisk' THEN 'AtRisk'
                                                WHEN SlaStatus IN ('OnTime', 'OnTrack') THEN 'OnTime'
                                                ELSE 'NoSla' END
                END AS GroupValue
            FROM gc_light
            WHERE (@Status   IS NULL OR AStatus = @Status)
              AND (@Priority IS NULL OR Priority = @Priority)
              -- @AppraisalNumber / @CustomerName / @Search are applied per-branch in gc_resolved (#f_cust/#f_appr/#f_search pushdown).
              AND (@RequestedAtFrom IS NULL OR RequestReceivedDate >= @RequestedAtFrom)
              AND (@RequestedAtTo   IS NULL OR RequestReceivedDate <  DATEADD(day, 1, @RequestedAtTo))
        )
        SELECT GroupValue AS [Value], COUNT(*) AS [Count]
        FROM gc
        WHERE GroupValue IS NOT NULL
        GROUP BY GroupValue
        OPTION (RECOMPILE, MAXDOP 1);
    END

    DROP TABLE #f_cust;
    DROP TABLE #f_appr;
    DROP TABLE #f_search;
END
-- NOTE: keep the access-scope/filter predicates here in lockstep with
-- workflow.sp_GetTaskList (same 4-branch routing) so group counts always
-- match the same population the paged /tasks/me and /tasks/pool queries see.
