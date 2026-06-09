-- =============================================================================
-- Parity test: vw_TaskList UNION-ALL rewrite vs. the live view
--
-- PURPOSE
--   Confirm that the rewritten vw_TaskList produces exactly the same rows as
--   the previous OUTER-APPLY + COALESCE implementation before swapping it in.
--
-- HOW TO RUN
--   1. Apply the NEW view under a temporary name (_new) by executing the
--      CREATE section below against your target database.
--   2. Run the EXCEPT checks — both must return 0 rows.
--   3. Run the COUNT check — both must return the same number.
--   4. Optionally run the per-task-type spot checks.
--   5. Run the DROP at the end to clean up.
--
-- NOTE: Do NOT apply this to production. Run against a populated test / INT db
--       that contains rows of all four task types (quotation, fee-approval,
--       document-followup, normal). If a task type has no data, note the gap
--       and create a representative fixture before signing off on parity.
-- =============================================================================

-- =============================================================================
-- STEP 1: Create the rewritten view under a temporary name.
--         (Same body as vw_TaskList but named vw_TaskList_new.)
-- =============================================================================
CREATE
OR ALTER
VIEW workflow.vw_TaskList_new
AS
WITH resolved AS (

    -- Branch 1: QUOTATION
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
           pt.SlaStatus,
           pt.WorkingBy,
           pt.LockedAt,
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

    -- Branch 2: FEE-APPROVAL
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
           pt.SlaStatus,
           pt.WorkingBy,
           pt.LockedAt,
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

    -- Branch 3: DOCUMENT-FOLLOWUP
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
           pt.SlaStatus,
           pt.WorkingBy,
           pt.LockedAt,
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

    -- Branch 4: NORMAL
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
           pt.SlaStatus,
           pt.WorkingBy,
           pt.LockedAt,
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
       resolved.AssignedAt                                                                      AS AssignedDate,
       resolved.Movement,
       AA.InternalAppraiserId                                                                   AS InternalFollowupStaff,
       CASE
           WHEN AA.AssignmentType = 'Internal' THEN AA.InternalAppraiserId
           WHEN AA.AssignmentType = 'External' THEN comp.Name
           END                                                                                  AS Appraiser,
       COALESCE(a.Priority, r.Priority)                                                         AS Priority,
       resolved.DueAt,
       resolved.SlaStatus,
       DATEDIFF(HOUR, resolved.AssignedAt, GETDATE())                                           AS ElapsedHours,
       CASE WHEN resolved.DueAt IS NOT NULL THEN DATEDIFF(HOUR, GETDATE(), resolved.DueAt) END  AS RemainingHours,
       resolved.WorkingBy,
       resolved.LockedAt,
       resolved.TaskId,
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
        SELECT TOP 1 Id, AssignmentType, InternalAppraiserId, AssigneeCompanyId
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
    LEFT JOIN auth.Companies   comp ON comp.Id   = TRY_CAST(AA.AssigneeCompanyId AS uniqueidentifier)
    LEFT JOIN auth.AspNetUsers u    ON u.UserName = ISNULL(a.RequestedBy, r.Requestor)
    LEFT JOIN auth.AspNetUsers qrm  ON qrm.UserName = resolved.RmUsername;
GO

-- =============================================================================
-- STEP 1b: ORPHAN PROBE — the ONE intentional divergence of the rewrite.
--   The new view INNER-JOINs each branch on its owning table, so a PendingTask
--   whose CorrelationId matches NONE of the four tables is dropped (the old view
--   kept it with NULL context). This MUST return 0 to certify the orphan-drop is
--   safe. If it returns rows, investigate before swapping the view (or add a
--   catch-all branch).
-- =============================================================================
PRINT '--- Orphan PendingTasks (CorrelationId matches no owning table) (expect 0 rows) ---';
SELECT pt.Id, pt.CorrelationId, pt.WorkflowInstanceId, pt.ActivityId
FROM   workflow.PendingTasks pt
WHERE  NOT EXISTS (SELECT 1 FROM appraisal.QuotationRequests       qr  WHERE qr.Id  = pt.CorrelationId)
  AND  NOT EXISTS (SELECT 1 FROM workflow.FeeAppointmentApprovals  faa WHERE faa.Id = pt.CorrelationId)
  AND  NOT EXISTS (SELECT 1 FROM workflow.DocumentFollowups        df  WHERE df.Id  = pt.CorrelationId)
  AND  NOT EXISTS (SELECT 1 FROM request.Requests                  r   WHERE r.Id   = pt.CorrelationId);

-- =============================================================================
-- STEP 2: Bidirectional EXCEPT — both must return 0 rows for full parity.
--
--   EXCEPT treats two NULLs as equal, so resolution drift (e.g. a column that
--   was previously NULL in old but non-NULL in new, or vice-versa) will surface
--   as a row in one of these result sets.
-- =============================================================================

-- Rows in OLD but not in NEW (old has something new is missing):
PRINT '--- Rows in OLD not in NEW (expect 0 rows) ---';
SELECT * FROM workflow.vw_TaskList
EXCEPT
SELECT * FROM workflow.vw_TaskList_new;

-- Rows in NEW but not in OLD (new has something old is missing):
PRINT '--- Rows in NEW not in OLD (expect 0 rows) ---';
SELECT * FROM workflow.vw_TaskList_new
EXCEPT
SELECT * FROM workflow.vw_TaskList;

-- =============================================================================
-- STEP 3: COUNT check — must be equal.
-- =============================================================================
PRINT '--- Row count comparison (must match) ---';
SELECT
    (SELECT COUNT(*) FROM workflow.vw_TaskList)     AS OldCount,
    (SELECT COUNT(*) FROM workflow.vw_TaskList_new) AS NewCount;

-- =============================================================================
-- STEP 4: Per-task-type spot checks.
--
--   Before running these, confirm your test database has at least one row of
--   each task type. If a type is missing, create a fixture or note the gap.
--
--   These queries compare the Id sets per task type between old and new to
--   confirm each branch resolves correctly. They should all return 0 rows.
-- =============================================================================

-- Quotation tasks: CorrelationId matches a QuotationRequests.Id
PRINT '--- Quotation task Id parity (expect 0 rows) ---';
SELECT Id FROM workflow.vw_TaskList     WHERE QuotationRequestId IS NOT NULL
EXCEPT
SELECT Id FROM workflow.vw_TaskList_new WHERE QuotationRequestId IS NOT NULL;

SELECT Id FROM workflow.vw_TaskList_new WHERE QuotationRequestId IS NOT NULL
EXCEPT
SELECT Id FROM workflow.vw_TaskList     WHERE QuotationRequestId IS NOT NULL;

-- Fee-approval tasks: TaskId matches a FeeAppointmentApprovals.Id (non-quotation, non-followup)
-- (Use TaskId IS NOT NULL AND QuotationRequestId IS NULL as the discriminator for fee-approval
--  and document-followup tasks combined; check TaskId values against the owning tables manually
--  or extend with an EXISTS subquery if needed.)
PRINT '--- Non-quotation TaskId parity (fee-approval + document-followup) (expect 0 rows) ---';
SELECT Id FROM workflow.vw_TaskList     WHERE TaskId IS NOT NULL AND QuotationRequestId IS NULL
EXCEPT
SELECT Id FROM workflow.vw_TaskList_new WHERE TaskId IS NOT NULL AND QuotationRequestId IS NULL;

SELECT Id FROM workflow.vw_TaskList_new WHERE TaskId IS NOT NULL AND QuotationRequestId IS NULL
EXCEPT
SELECT Id FROM workflow.vw_TaskList     WHERE TaskId IS NOT NULL AND QuotationRequestId IS NULL;

-- Normal tasks: TaskId IS NULL
PRINT '--- Normal task parity (expect 0 rows) ---';
SELECT Id FROM workflow.vw_TaskList     WHERE TaskId IS NULL
EXCEPT
SELECT Id FROM workflow.vw_TaskList_new WHERE TaskId IS NULL;

SELECT Id FROM workflow.vw_TaskList_new WHERE TaskId IS NULL
EXCEPT
SELECT Id FROM workflow.vw_TaskList     WHERE TaskId IS NULL;

-- =============================================================================
-- STEP 5: Cleanup — drop the temporary view.
-- =============================================================================
DROP VIEW IF EXISTS workflow.vw_TaskList_new;
PRINT '--- vw_TaskList_new dropped. Parity test complete. ---';
