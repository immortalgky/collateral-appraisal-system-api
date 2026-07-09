CREATE
OR ALTER
VIEW workflow.vw_TaskList
AS
-- =============================================================================
-- vw_TaskList — one row per PendingTask with display enrichment.
--
-- Architecture: UNION ALL of one branch per task type (discriminated by
-- CorrelationId), followed by shared display enrichment applied once on top.
--
-- CorrelationId is a discriminated-union key: it equals the PK of exactly ONE
-- owning table per task type:
--   Branch 1 QUOTATION        — appraisal.QuotationRequests.Id
--   Branch 2 FEE-APPROVAL     — workflow.FeeAppointmentApprovals.Id
--   Branch 3 DOCUMENT-FOLLOWUP — workflow.DocumentFollowups.Id
--   Branch 4 NORMAL           — request.Requests.Id
--
-- GUIDs are globally unique across tables, so every PendingTask falls into
-- exactly ONE branch. No NOT-EXISTS guards are needed.
--
-- PARITY NOTE: INNER JOINs are used in each branch (vs. the previous OUTER
-- APPLYs), so an orphan PendingTask whose CorrelationId matches none of the
-- four tables is dropped. This is the expected-empty case; the EXCEPT parity
-- test confirms. If orphans legitimately exist, add a catch-all branch.
-- =============================================================================
WITH resolved AS (

    -- -------------------------------------------------------------------------
    -- Branch 1: QUOTATION task
    --   CorrelationId = appraisal.QuotationRequests.Id
    --   AppraisalId resolved via the first QuotationRequestAppraisals row.
    -- -------------------------------------------------------------------------
    SELECT pt.Id,
           pt.CorrelationId,
           pt.WorkflowInstanceId,
           pt.ActivityId,
           pt.TaskName,
           pt.TaskDescription,
           pt.TaskStatus,
           pt.AssignedTo,
           pt.AssignedType,
           pt.AssigneeCompanyId,
           pt.AssignedAt,
           pt.Movement,
           pt.DueAt,
           pt.SlaStartAt,
           pt.SlaStatus,
           pt.SlaDurationHours,
           pt.WorkingBy,
           pt.LockedAt,
           -- Type-resolution columns
           qra.AppraisalId                      AS RAppraisalId,
           CAST(NULL AS uniqueidentifier)        AS RRequestIdOverride,
           qr.Id                                AS QuotationRequestId,
           qr.QuotationNumber,
           qr.Status                            AS QuotationStatus,
           qr.CutOffTime                        AS QuotationCutOffTime,
           qr.RmUsername,
           qr.Id                                AS TaskId
    FROM   workflow.PendingTasks pt
    JOIN   appraisal.QuotationRequests qr ON qr.Id = pt.CorrelationId
    OUTER APPLY (
        SELECT TOP 1 AppraisalId
        FROM   appraisal.QuotationRequestAppraisals
        WHERE  QuotationRequestId = qr.Id
        ORDER BY AppraisalId
    ) qra

    UNION ALL

    -- -------------------------------------------------------------------------
    -- Branch 2: FEE-APPROVAL task
    --   CorrelationId = workflow.FeeAppointmentApprovals.Id
    --   AppraisalId resolved directly from the FeeAppointmentApprovals row.
    -- -------------------------------------------------------------------------
    SELECT pt.Id,
           pt.CorrelationId,
           pt.WorkflowInstanceId,
           pt.ActivityId,
           pt.TaskName,
           pt.TaskDescription,
           pt.TaskStatus,
           pt.AssignedTo,
           pt.AssignedType,
           pt.AssigneeCompanyId,
           pt.AssignedAt,
           pt.Movement,
           pt.DueAt,
           pt.SlaStartAt,
           pt.SlaStatus,
           pt.SlaDurationHours,
           pt.WorkingBy,
           pt.LockedAt,
           -- Type-resolution columns
           faa.AppraisalId                      AS RAppraisalId,
           CAST(NULL AS uniqueidentifier)        AS RRequestIdOverride,
           CAST(NULL AS uniqueidentifier)        AS QuotationRequestId,
           CAST(NULL AS nvarchar(50))            AS QuotationNumber,
           CAST(NULL AS nvarchar(50))            AS QuotationStatus,
           CAST(NULL AS datetime2)               AS QuotationCutOffTime,
           CAST(NULL AS nvarchar(50))            AS RmUsername,
           faa.Id                               AS TaskId
    FROM   workflow.PendingTasks pt
    JOIN   workflow.FeeAppointmentApprovals faa ON faa.Id = pt.CorrelationId

    UNION ALL

    -- -------------------------------------------------------------------------
    -- Branch 3: DOCUMENT-FOLLOWUP task
    --   CorrelationId = workflow.DocumentFollowups.Id
    --   AppraisalId and RequestId resolved directly from the DocumentFollowups row.
    -- -------------------------------------------------------------------------
    SELECT pt.Id,
           pt.CorrelationId,
           pt.WorkflowInstanceId,
           pt.ActivityId,
           pt.TaskName,
           pt.TaskDescription,
           pt.TaskStatus,
           pt.AssignedTo,
           pt.AssignedType,
           pt.AssigneeCompanyId,
           pt.AssignedAt,
           pt.Movement,
           pt.DueAt,
           pt.SlaStartAt,
           pt.SlaStatus,
           pt.SlaDurationHours,
           pt.WorkingBy,
           pt.LockedAt,
           -- Type-resolution columns
           df.AppraisalId                       AS RAppraisalId,
           df.RequestId                         AS RRequestIdOverride,
           CAST(NULL AS uniqueidentifier)        AS QuotationRequestId,
           CAST(NULL AS nvarchar(50))            AS QuotationNumber,
           CAST(NULL AS nvarchar(50))            AS QuotationStatus,
           CAST(NULL AS datetime2)               AS QuotationCutOffTime,
           CAST(NULL AS nvarchar(50))            AS RmUsername,
           df.Id                                AS TaskId
    FROM   workflow.PendingTasks pt
    JOIN   workflow.DocumentFollowups df ON df.Id = pt.CorrelationId

    UNION ALL

    -- -------------------------------------------------------------------------
    -- Branch 4: NORMAL appraisal-workflow task
    --   CorrelationId = request.Requests.Id
    --   AppraisalId resolved via Appraisals (first by Id for this RequestId).
    --   RRequestIdOverride = CorrelationId (it IS the RequestId).
    -- -------------------------------------------------------------------------
    SELECT pt.Id,
           pt.CorrelationId,
           pt.WorkflowInstanceId,
           pt.ActivityId,
           pt.TaskName,
           pt.TaskDescription,
           pt.TaskStatus,
           pt.AssignedTo,
           pt.AssignedType,
           pt.AssigneeCompanyId,
           pt.AssignedAt,
           pt.Movement,
           pt.DueAt,
           pt.SlaStartAt,
           pt.SlaStatus,
           pt.SlaDurationHours,
           pt.WorkingBy,
           pt.LockedAt,
           -- Type-resolution columns
           (SELECT TOP 1 a3.Id
            FROM   appraisal.Appraisals a3
            WHERE  a3.RequestId = pt.CorrelationId
            ORDER BY a3.Id)                     AS RAppraisalId,
           pt.CorrelationId                     AS RRequestIdOverride,
           CAST(NULL AS uniqueidentifier)        AS QuotationRequestId,
           CAST(NULL AS nvarchar(50))            AS QuotationNumber,
           CAST(NULL AS nvarchar(50))            AS QuotationStatus,
           CAST(NULL AS datetime2)               AS QuotationCutOffTime,
           CAST(NULL AS nvarchar(50))            AS RmUsername,
           CAST(NULL AS uniqueidentifier)        AS TaskId
    FROM   workflow.PendingTasks pt
    JOIN   request.Requests r ON r.Id = pt.CorrelationId

)
-- =============================================================================
-- Outer query: shared display enrichment applied once across all task types.
-- Produces the same 38 output columns (same names, order, and semantics) as the
-- previous OUTER-APPLY + COALESCE implementation.
-- =============================================================================
SELECT resolved.Id                                                                              AS Id,
       a.Id                                                                                     AS AppraisalId,
       r.Id                                                                                     AS RequestId,
       resolved.WorkflowInstanceId,
       resolved.ActivityId,
       a.AppraisalNumber,
       r.RequestNumber,
       c.Name                                                                                   AS CustomerName,
       resolved.TaskName                                                                        AS TaskType,
       resolved.TaskDescription,
       r.Purpose,
       p.PropertyType                                                                           AS PropertyType,
       a.Status                                                                                 AS Status,
       resolved.TaskStatus                                                                      AS PendingTaskStatus,
       ap.AppointmentDateTime,
       resolved.AssignedTo                                                                      AS AssigneeUserId,
       resolved.AssignedType                                                                    AS AssignedType,
       resolved.AssigneeCompanyId                                                               AS AssigneeCompanyId,
       COALESCE(a.RequestedBy, r.Requestor)                                                     AS RequestedBy,
       COALESCE(CONCAT(u.FirstName, ' ', u.LastName), ISNULL(a.RequestedBy, r.Requestor))       AS RequestedByName,
       COALESCE(a.RequestedAt, r.RequestedAt)                                                   AS RequestReceivedDate,
       -- Requested At: when the request was submitted (request-side submit timestamp).
       COALESCE(a.RequestedAt, r.RequestedAt)                                                   AS RequestedAt,
       -- Report received: external appraiser's first handoff (InProgress -> UnderReview),
       -- same source/semantics as vw_AppraisalEvaluation{Header,List}.ReportReceivedDate.
       rr.SubmittedAt                                                                           AS ReportReceivedAt,
       resolved.AssignedAt                                                                      AS AssignedDate,
       resolved.Movement,
       -- Internal follow-up staff = InternalAppraiserId; resolve the code to a full name.
       COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(ifs.FirstName, ' ', ifs.LastName))), ''),
                AA.InternalAppraiserId)                                                         AS InternalFollowupStaff,
       -- Appraiser (internal) = AssigneeUserId (the assigned internal appraiser), NOT the
       -- follow-up staff; resolve to a full name. External = company name.
       CASE
           WHEN AA.AssignmentType = 'Internal' THEN COALESCE(
                NULLIF(LTRIM(RTRIM(CONCAT(au.FirstName, ' ', au.LastName))), ''),
                AA.AssigneeUserId)
           WHEN AA.AssignmentType = 'External' THEN comp.Name
           END                                                                                  AS Appraiser,
       COALESCE(a.Priority, r.Priority)                                                         AS Priority,
       resolved.DueAt,
       resolved.SlaStartAt,
       resolved.SlaStatus,
       resolved.SlaDurationHours,
       -- ElapsedHours / RemainingHours are computed in C# (GetTasksQueryHandler) using
       -- IBusinessTimeCalculator so they exclude weekends, holidays and lunch. They are NOT
       -- derived here: a SQL DATEDIFF would count calendar hours (nights/weekends included).
       resolved.WorkingBy,
       resolved.LockedAt,
       -- TaskId: the domain entity id that FE should route to.
       -- Quotation tasks use the QuotationRequestId; document-followup tasks use DocumentFollowup.Id;
       -- fee-appointment approval tasks use FeeAppointmentApproval.Id;
       -- regular tasks have NULL (FE falls back to workflowInstanceId, preserving legacy behaviour).
       resolved.TaskId,
       -- Quotation context columns (NULL for non-quotation tasks)
       resolved.QuotationRequestId,
       resolved.QuotationNumber,
       resolved.QuotationStatus,
       resolved.QuotationCutOffTime,
       qrm.FirstName + ' ' + qrm.LastName                                                      AS QuotationRmName
FROM   resolved
    LEFT JOIN appraisal.Appraisals a   ON a.Id  = resolved.RAppraisalId
    LEFT JOIN request.Requests     r   ON r.Id  = COALESCE(resolved.RRequestIdOverride, a.RequestId)
    OUTER APPLY (
        SELECT TOP 1 Name
        FROM   request.RequestCustomers
        WHERE  RequestId = r.Id
    ) c
    OUTER APPLY (
        SELECT STRING_AGG(PropertyType, ',') AS PropertyType
        FROM   request.RequestProperties
        WHERE  RequestId = r.Id
        GROUP BY RequestId
    ) p
    OUTER APPLY (
        SELECT TOP 1 Id, AssignmentType, AssigneeUserId, InternalAppraiserId, AssigneeCompanyId
        FROM   appraisal.AppraisalAssignments
        WHERE  AppraisalId = a.Id
          AND  AssignmentStatus NOT IN ('Rejected', 'Cancelled')
        ORDER BY AssignedAt DESC, CreatedAt DESC, Id DESC
    ) AA
    OUTER APPLY (
        SELECT TOP 1 AppointmentDateTime
        FROM   appraisal.Appointments
        WHERE  AssignmentId = AA.Id
          AND  Status != 'Cancelled'
    ) ap
    OUTER APPLY (
        -- Most-recent external appraiser assignment's submission timestamp (report received).
        SELECT TOP 1 SubmittedAt
        FROM   appraisal.AppraisalAssignments
        WHERE  AppraisalId = a.Id
          AND  AssignmentType = 'External'
          AND  AssignmentStatus NOT IN ('Rejected', 'Cancelled')
        ORDER BY AssignedAt DESC, CreatedAt DESC, Id DESC
    ) rr
    LEFT JOIN auth.Companies   comp ON comp.Id   = TRY_CAST(AA.AssigneeCompanyId AS uniqueidentifier)
    LEFT JOIN auth.AspNetUsers u    ON u.UserName = ISNULL(a.RequestedBy, r.Requestor)
    LEFT JOIN auth.AspNetUsers ifs  ON ifs.UserName = AA.InternalAppraiserId
    LEFT JOIN auth.AspNetUsers au   ON au.UserName  = AA.AssigneeUserId
    LEFT JOIN auth.AspNetUsers qrm  ON qrm.UserName = resolved.RmUsername;
