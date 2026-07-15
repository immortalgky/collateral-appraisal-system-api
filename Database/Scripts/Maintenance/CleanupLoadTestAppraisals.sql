/*==============================================================================
  CleanupLoadTestAppraisals.sql
  ------------------------------------------------------------------------------
  Purpose : Hard-delete the OLDEST ~50% of appraisals and EVERY dependent row
            across the appraisal / request / workflow (and correlated
            notification) schemas in a LOAD-TEST database, keeping the newest
            half for continued testing.

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations.

  WHY it is hand-ordered (not a single parent DELETE):
    * The modules are separate DbContexts with NO cross-schema foreign keys.
      Deletes do not cascade between schemas. Rows are correlated only by
      logical Guids:
        - appraisal.Appraisals.RequestId              = request.Requests.Id
        - workflow core tables .CorrelationId          = RequestId
          (nvarchar for WorkflowInstances/ExecutionLogs/Outboxes/Bookmarks;
           uniqueidentifier for PendingTasks/CompletedTasks/SlaBreachLogs)
        - workflow committee/meeting/fee/vote tables    use AppraisalId directly
        - workflow quotation child-workflow .CorrelationId = QuotationRequestId
    * Cascade only helps INSIDE an aggregate (e.g. deleting an Appraisals row
      cascades to its property/detail/assignment/fee/project trees). ~20
      appraisal tables and all workflow correlated tables have loose Guid
      columns with no FK -> they must be deleted explicitly or they orphan.
    * RESTRICT / NoAction constraints (Quotation, PropertyPhotoMappings,
      GalleryPhotoTopicMappings, Appointments, assignment self-ref,
      Projects-internal) would make a plain root delete THROW, so we clear /
      neutralise them first.

  SAFETY:
    * Everything runs inside ONE transaction. The script STOPS before COMMIT.
    * Section 1 prints per-table row counts (dry run). Review them.
    * Section 7 prints post-delete counts + orphan checks (should all be 0).
    * Then YOU issue COMMIT TRANN or ROLLBACK TRAN at the very bottom.
    * Recommended first pass: run through, review, then ROLLBACK. Re-run and
      COMMIT once the numbers look right.

  Re-runnable: uses #temp tables only; safe to re-execute after ROLLBACK.
==============================================================================*/

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- any error aborts the whole transaction

-- REQUIRED: several target tables carry filtered and/or spatial indexes
-- (e.g. PERSISTED geography + spatial index). DML against them fails with
-- Msg 1934 unless these session options are ON. Some clients (sqlcmd) default
-- QUOTED_IDENTIFIER OFF, so set them explicitly here.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-------------------------------------------------------------------------------
-- CONFIG
-------------------------------------------------------------------------------
DECLARE @DeletePercent int = 50;   -- delete the OLDEST @DeletePercent % of appraisals

BEGIN TRAN;

/*==============================================================================
  SECTION 0 - Build driver temp tables (the target set + helper id sets)
==============================================================================*/

-- 0.1  Target appraisals = oldest @DeletePercent %.
--      Ordered by CreatedAt (audit column) then Id for a stable tie-break.
IF OBJECT_ID('tempdb..#TargetAppr') IS NOT NULL DROP TABLE #TargetAppr;
SELECT TOP (@DeletePercent) PERCENT
       a.Id        AS AppraisalId,
       a.RequestId AS RequestId
INTO   #TargetAppr
FROM   appraisal.Appraisals a
ORDER  BY a.CreatedAt, a.Id;

CREATE UNIQUE CLUSTERED INDEX IX_#TargetAppr ON #TargetAppr(AppraisalId);

-- 0.2  Requests that are FULLY covered (every appraisal of the request is in the
--      target set). Only these requests + their RequestId-correlated workflow
--      rows are safe to delete; a surviving reappraisal keeps its request.
IF OBJECT_ID('tempdb..#TargetReq') IS NOT NULL DROP TABLE #TargetReq;
SELECT DISTINCT t.RequestId
INTO   #TargetReq
FROM   #TargetAppr t
WHERE  NOT EXISTS (
         SELECT 1 FROM appraisal.Appraisals a2
         WHERE  a2.RequestId = t.RequestId
           AND  NOT EXISTS (SELECT 1 FROM #TargetAppr x WHERE x.AppraisalId = a2.Id));
CREATE UNIQUE CLUSTERED INDEX IX_#TargetReq ON #TargetReq(RequestId);

-- 0.3  Helper id sets scoped to the target appraisals -------------------------

-- Appraisal properties (for PropertyPhotoMappings blocker)
IF OBJECT_ID('tempdb..#TargetProp') IS NOT NULL DROP TABLE #TargetProp;
SELECT p.Id AS AppraisalPropertyId
INTO   #TargetProp
FROM   appraisal.AppraisalProperties p
JOIN   #TargetAppr t ON t.AppraisalId = p.AppraisalId;

-- Assignments (for Appointments blocker + InvoiceItems)
IF OBJECT_ID('tempdb..#TargetAssign') IS NOT NULL DROP TABLE #TargetAssign;
SELECT s.Id AS AssignmentId
INTO   #TargetAssign
FROM   appraisal.AppraisalAssignments s
JOIN   #TargetAppr t ON t.AppraisalId = s.AppraisalId;

-- Reviews (for CommitteeVotes)
IF OBJECT_ID('tempdb..#TargetReview') IS NOT NULL DROP TABLE #TargetReview;
SELECT r.Id AS ReviewId
INTO   #TargetReview
FROM   appraisal.AppraisalReviews r
JOIN   #TargetAppr t ON t.AppraisalId = r.AppraisalId;

-- Gallery photos + photo topics (for GalleryPhotoTopicMappings / PropertyPhotoMappings)
IF OBJECT_ID('tempdb..#TargetGalleryPhoto') IS NOT NULL DROP TABLE #TargetGalleryPhoto;
SELECT g.Id AS GalleryPhotoId
INTO   #TargetGalleryPhoto
FROM   appraisal.AppraisalGallery g
JOIN   #TargetAppr t ON t.AppraisalId = g.AppraisalId;

IF OBJECT_ID('tempdb..#TargetPhotoTopic') IS NOT NULL DROP TABLE #TargetPhotoTopic;
SELECT pt.Id AS PhotoTopicId
INTO   #TargetPhotoTopic
FROM   appraisal.PhotoTopics pt
JOIN   #TargetAppr t ON t.AppraisalId = pt.AppraisalId;

-- Projects + pricing anchors (PricingAnalysis.AnchorId = PropertyGroups.Id or ProjectModels.Id)
IF OBJECT_ID('tempdb..#TargetProject') IS NOT NULL DROP TABLE #TargetProject;
SELECT pj.Id AS ProjectId
INTO   #TargetProject
FROM   appraisal.Projects pj
JOIN   #TargetAppr t ON t.AppraisalId = pj.AppraisalId;

IF OBJECT_ID('tempdb..#TargetAnchor') IS NOT NULL DROP TABLE #TargetAnchor;
SELECT pg.Id AS AnchorId
INTO   #TargetAnchor
FROM   appraisal.PropertyGroups pg
JOIN   #TargetAppr t ON t.AppraisalId = pg.AppraisalId
UNION
SELECT pm.Id
FROM   appraisal.ProjectModels pm
JOIN   #TargetProject tp ON tp.ProjectId = pm.ProjectId;

-- Quotation requests that are FULLY covered (all their appraisals targeted) ->
-- delete whole (cascade). Partially-covered ones are only unlinked (section 3).
IF OBJECT_ID('tempdb..#TargetQuotation') IS NOT NULL DROP TABLE #TargetQuotation;
SELECT DISTINCT qra.QuotationRequestId
INTO   #TargetQuotation
FROM   appraisal.QuotationRequestAppraisals qra
JOIN   #TargetAppr t ON t.AppraisalId = qra.AppraisalId
WHERE  NOT EXISTS (
         SELECT 1 FROM appraisal.QuotationRequestAppraisals qra2
         WHERE  qra2.QuotationRequestId = qra.QuotationRequestId
           AND  NOT EXISTS (SELECT 1 FROM #TargetAppr x WHERE x.AppraisalId = qra2.AppraisalId));

-- 0.4  Correlation-id string set = string forms of RequestIds (fully covered)
--      + AppraisalIds (all target) + QuotationRequestIds (fully covered).
--      Used for the nvarchar CorrelationId columns (WorkflowInstances, outboxes).
IF OBJECT_ID('tempdb..#CorrStr') IS NOT NULL DROP TABLE #CorrStr;
CREATE TABLE #CorrStr (Corr nvarchar(200) NOT NULL PRIMARY KEY);
INSERT #CorrStr (Corr)
SELECT CONVERT(nvarchar(36), RequestId) FROM #TargetReq
UNION
SELECT CONVERT(nvarchar(36), QuotationRequestId) FROM #TargetQuotation;

-- Workflow instance ids for the target correlations (for ActivityProcessExecutions,
-- which is decoupled and keyed only by WorkflowInstanceId).
IF OBJECT_ID('tempdb..#TargetWfInstance') IS NOT NULL DROP TABLE #TargetWfInstance;
SELECT wi.Id AS WorkflowInstanceId
INTO   #TargetWfInstance
FROM   workflow.WorkflowInstances wi
JOIN   #CorrStr c ON c.Corr = wi.CorrelationId;


/*==============================================================================
  SECTION 1 - DRY RUN: row counts that WILL be deleted. Review before committing.
==============================================================================*/
PRINT '================ DRY-RUN COUNTS (rows that will be deleted) ================';

SELECT 'appraisal.Appraisals (target)'      AS TableName, COUNT(*) AS Rows FROM #TargetAppr
UNION ALL SELECT 'request.Requests (fully covered)', COUNT(*) FROM #TargetReq
UNION ALL SELECT '  workflow.WorkflowInstances',    COUNT(*) FROM workflow.WorkflowInstances wi JOIN #CorrStr c ON c.Corr = wi.CorrelationId
UNION ALL SELECT '  workflow.PendingTasks',         COUNT(*) FROM workflow.PendingTasks   x JOIN #TargetReq t ON t.RequestId = x.CorrelationId
UNION ALL SELECT '  workflow.CompletedTasks',       COUNT(*) FROM workflow.CompletedTasks x JOIN #TargetReq t ON t.RequestId = x.CorrelationId
UNION ALL SELECT '  workflow.SlaBreachLogs',        COUNT(*) FROM workflow.SlaBreachLogs  x JOIN #TargetReq t ON t.RequestId = x.CorrelationId
UNION ALL SELECT '  workflow.ApprovalVotes',        COUNT(*) FROM workflow.ApprovalVotes  x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL SELECT '  workflow.FeeAppointmentApprovals', COUNT(*) FROM workflow.FeeAppointmentApprovals x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL SELECT '  workflow.MeetingItems',         COUNT(*) FROM workflow.MeetingItems   x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL SELECT '  workflow.MeetingQueueItems',    COUNT(*) FROM workflow.MeetingQueueItems x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL SELECT '  workflow.AppraisalAcknowledgementQueueItems', COUNT(*) FROM workflow.AppraisalAcknowledgementQueueItems x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL SELECT '  workflow.DocumentFollowups',    COUNT(*) FROM workflow.DocumentFollowups x WHERE EXISTS (SELECT 1 FROM #TargetAppr t WHERE t.AppraisalId = x.AppraisalId) OR EXISTS (SELECT 1 FROM #TargetReq r WHERE r.RequestId = x.RequestId)
UNION ALL SELECT '  appraisal.QuotationRequests (full)', COUNT(*) FROM #TargetQuotation
UNION ALL SELECT '  appraisal.PricingAnalysis',     COUNT(*) FROM appraisal.PricingAnalysis x JOIN #TargetAnchor a ON a.AnchorId = x.AnchorId
UNION ALL SELECT '  appraisal.AppraisalReviews',    COUNT(*) FROM appraisal.AppraisalReviews x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL SELECT '  appraisal.Appointments',        COUNT(*) FROM appraisal.Appointments x JOIN #TargetAssign s ON s.AssignmentId = x.AssignmentId
ORDER BY TableName;

PRINT '===========================================================================';


/*==============================================================================
  SECTION 2 - DELETE: workflow schema
  (decoupled from appraisal/request: must delete explicitly by correlation)
==============================================================================*/

-- 2.1  Decoupled task/sla/process tables (no FK) - CorrelationId = RequestId (Guid)
DELETE x FROM workflow.PendingTasks    x JOIN #TargetReq t ON t.RequestId = x.CorrelationId;
DELETE x FROM workflow.CompletedTasks  x JOIN #TargetReq t ON t.RequestId = x.CorrelationId;
DELETE x FROM workflow.SlaBreachLogs   x JOIN #TargetReq t ON t.RequestId = x.CorrelationId;
DELETE x FROM workflow.ActivityProcessExecutions x JOIN #TargetWfInstance w ON w.WorkflowInstanceId = x.WorkflowInstanceId;

-- 2.2  Appraisal-keyed workflow tables (no FK) - AppraisalId
DELETE x FROM workflow.ApprovalVotes                     x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM workflow.FeeAppointmentApprovals           x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;  -- cascades FeeAppointmentApprovalLines
DELETE x FROM workflow.MeetingItems                      x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;  -- shared Meetings header left intact
DELETE x FROM workflow.MeetingQueueItems                 x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM workflow.AppraisalAcknowledgementQueueItems x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM workflow.DocumentFollowups x
  WHERE EXISTS (SELECT 1 FROM #TargetAppr t WHERE t.AppraisalId = x.AppraisalId)
     OR EXISTS (SELECT 1 FROM #TargetReq  r WHERE r.RequestId  = x.RequestId);

-- 2.3  Outboxes correlated by RequestId string (SetNull on instance delete leaves rows)
DELETE x FROM workflow.WorkflowOutboxes x JOIN #CorrStr c ON c.Corr = x.CorrelationId;

-- 2.4  Root instance delete (cascades WorkflowActivityExecutions, WorkflowBookmarks,
--      WorkflowExecutionLogs, WorkflowExternalCalls)
DELETE x FROM workflow.WorkflowInstances x JOIN #CorrStr c ON c.Corr = x.CorrelationId;


/*==============================================================================
  SECTION 3 - DELETE: appraisal schema
  Order: neutralise self/NoAction FKs -> clear RESTRICT blockers ->
         delete loose per-appraisal tables -> root cascade delete.
==============================================================================*/

-- 3.1  Neutralise NoAction / self-reference FKs (all nullable) so the later
--      cascade delete cannot hit an ordering conflict.
UPDATE s SET s.PreviousAssignmentId = NULL
FROM appraisal.AppraisalAssignments s JOIN #TargetAppr t ON t.AppraisalId = s.AppraisalId
WHERE s.PreviousAssignmentId IS NOT NULL;

UPDATE u SET u.ProjectModelId = NULL, u.ProjectTowerId = NULL
FROM appraisal.ProjectUnits u JOIN #TargetProject tp ON tp.ProjectId = u.ProjectId
WHERE u.ProjectModelId IS NOT NULL OR u.ProjectTowerId IS NOT NULL;

UPDATE m SET m.ProjectTowerId = NULL
FROM appraisal.ProjectModels m JOIN #TargetProject tp ON tp.ProjectId = m.ProjectId
WHERE m.ProjectTowerId IS NOT NULL;

-- 3.2  RESTRICT / NoAction blockers - delete children before their parents.

-- Quotation: delete fully-covered quotation requests whole (cascade removes
-- Items, Invitations, CompanyQuotations(+Items+Negotiations), Appraisals join,
-- SharedDocuments). Loose logs/emails deleted explicitly.
DELETE x FROM appraisal.QuotationActivityLogs x JOIN #TargetQuotation q ON q.QuotationRequestId = x.QuotationRequestId;
DELETE x FROM appraisal.QuotationEmails       x JOIN #TargetQuotation q ON q.QuotationRequestId = x.QuotationRequestId;
DELETE x FROM appraisal.QuotationRequests     x JOIN #TargetQuotation q ON q.QuotationRequestId = x.Id;
-- Partially-covered quotations: just unlink target appraisals + drop their loose rows.
DELETE x FROM appraisal.QuotationRequestAppraisals x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.QuotationRequestItems       x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.QuotationSharedDocuments     x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.CompanyQuotationItems        x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;

-- Gallery mapping blockers (Restrict) - before deleting AppraisalGallery / PhotoTopics / AppraisalProperties.
DELETE x FROM appraisal.GalleryPhotoTopicMappings x
  WHERE EXISTS (SELECT 1 FROM #TargetGalleryPhoto g WHERE g.GalleryPhotoId = x.GalleryPhotoId)
     OR EXISTS (SELECT 1 FROM #TargetPhotoTopic  p WHERE p.PhotoTopicId  = x.PhotoTopicId);
DELETE x FROM appraisal.PropertyPhotoMappings x JOIN #TargetProp p ON p.AppraisalPropertyId = x.AppraisalPropertyId;

-- Appointments (NoAction on AssignmentId) - before root cascade removes assignments.
-- AppointmentHistory cascades from Appointments.
DELETE x FROM appraisal.Appointments x JOIN #TargetAssign s ON s.AssignmentId = x.AssignmentId;

-- 3.3  Loose per-appraisal tables (each cascades its own children).
DELETE x FROM appraisal.PricingAnalysis x JOIN #TargetAnchor a ON a.AnchorId = x.AnchorId;   -- cascades whole pricing tree
DELETE x FROM appraisal.CommitteeVotes  x JOIN #TargetReview r ON r.ReviewId = x.ReviewId;
DELETE x FROM appraisal.AppraisalReviews          x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.AppraisalDecisions        x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.ValuationAnalyses         x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.AppraisalEvaluations      x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.MachineryAppraisalSummaries x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.AssetSummary              x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.AssetSummaryGroup         x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.AppraisalComparables      x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;  -- cascades ComparableAdjustments
DELETE x FROM appraisal.AppraisalAppendices       x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;  -- cascades AppendixDocuments
DELETE x FROM appraisal.LawAndRegulations         x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;  -- cascades images
DELETE x FROM appraisal.AppraisalGallery          x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
DELETE x FROM appraisal.PhotoTopics               x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId;
-- Invoice LINE items only (per-assignment). Invoices are per-company batches
-- shared across appraisals - do NOT delete the Invoices headers.
DELETE x FROM appraisal.InvoiceItems              x JOIN #TargetAssign s ON s.AssignmentId = x.AssignmentId;

-- 3.4  ROOT DELETE. Cascades: AppraisalProperties(+all owned detail trees),
--      AppraisalAssignments(+AppraisalFees+Items+PaymentHistory), PropertyGroups
--      (+Items), Projects(+Lands/Towers/Models/Units and their children).
DELETE x FROM appraisal.Appraisals x JOIN #TargetAppr t ON t.AppraisalId = x.Id;


/*==============================================================================
  SECTION 4 - DELETE: request schema (only fully-covered requests)
==============================================================================*/
-- RequestTitles / RequestComments reference RequestId by an unconstrained column
-- (no FK) -> delete explicitly. RequestTitles cascades RequestTitleDocuments.
DELETE x FROM request.RequestTitles   x JOIN #TargetReq t ON t.RequestId = x.RequestId;
DELETE x FROM request.RequestComments x JOIN #TargetReq t ON t.RequestId = x.RequestId;
-- Root: cascades RequestDetails, RequestCustomers, RequestProperties, RequestDocuments.
DELETE x FROM request.Requests        x JOIN #TargetReq t ON t.RequestId = x.Id;


/*==============================================================================
  SECTION 5 - Noise / decoupled correlated tables (correlation-based prune)
==============================================================================*/
-- IntegrationEventOutbox has a free-form nvarchar CorrelationId that carries a
-- RequestId / AppraisalId / QuotationRequestId string. Prune the rows for the
-- deleted set. (#CorrStr already holds RequestId + QuotationRequestId strings;
-- add the AppraisalId strings here.)
INSERT #CorrStr (Corr)
SELECT CONVERT(nvarchar(36), AppraisalId) FROM #TargetAppr
WHERE  CONVERT(nvarchar(36), AppraisalId) NOT IN (SELECT Corr FROM #CorrStr);

DELETE x FROM workflow.IntegrationEventOutbox x JOIN #CorrStr c ON c.Corr = x.CorrelationId;
DELETE x FROM request.IntegrationEventOutbox  x JOIN #CorrStr c ON c.Corr = x.CorrelationId;


/*==============================================================================
  SECTION 6 - OPTIONAL: keyless high-volume tables (NO joinable correlation).
  ----------------------------------------------------------------------------
  These accumulate under load but have no clean key back to an appraisal:
    * workflow.InboxMessage / request.InboxMessage / notification.InboxMessage
      -> MassTransit-style dedupe tables (MessageId, ConsumerType).
    * notification.EmailSendLogs  -> free-form ReferenceId, CreatedAt.
    * notification.UserNotifications -> UserId(username); ref only inside
      Metadata / ActionUrl strings.
  They are best pruned by a TIME CUTOFF, not by the 50% set. This block is
  COMMENTED OUT by default. To enable, set @Cutoff (e.g. the newest CreatedAt
  among deleted appraisals) and uncomment. Review carefully - this deletes
  processed log/dedupe rows OLDER than the cutoff regardless of appraisal.
==============================================================================*/
/*
-- NOTE: the root Appraisals rows are already gone by now, so derive the cutoff
--       from the request rows we kept a copy of, or hard-code a known timestamp.
--       Simplest: capture @Cutoff in Section 0 (before deletes) into a variable
--       that survives, then reference it here.
DECLARE @Cutoff datetime2 = '2026-01-01T00:00:00';   -- <-- set to your intended cutoff
DELETE FROM notification.UserNotifications WHERE CreatedAt <= @Cutoff AND IsRead = 1;
DELETE FROM notification.EmailSendLogs     WHERE CreatedAt <= @Cutoff;
DELETE FROM notification.InboxMessage      WHERE Received  <= @Cutoff;   -- verify column name
DELETE FROM workflow.InboxMessage          WHERE Received  <= @Cutoff;   -- verify column name
DELETE FROM request.InboxMessage           WHERE Received  <= @Cutoff;   -- verify column name
*/


/*==============================================================================
  SECTION 7 - POST-DELETE verification (run before COMMIT)
==============================================================================*/
PRINT '================ POST-DELETE VERIFICATION ================';

SELECT 'appraisal.Appraisals remaining' AS Metric, COUNT(*) AS Value FROM appraisal.Appraisals
UNION ALL SELECT 'request.Requests remaining', COUNT(*) FROM request.Requests;

-- Orphan checks - every count below MUST be 0.
SELECT 'ORPHAN appraisal.AppraisalReviews' AS Check_MustBeZero, COUNT(*) AS Cnt
FROM appraisal.AppraisalReviews r
WHERE NOT EXISTS (SELECT 1 FROM appraisal.Appraisals a WHERE a.Id = r.AppraisalId)
UNION ALL
SELECT 'ORPHAN appraisal.AppraisalProperties', COUNT(*)
FROM appraisal.AppraisalProperties p
WHERE NOT EXISTS (SELECT 1 FROM appraisal.Appraisals a WHERE a.Id = p.AppraisalId)
UNION ALL
SELECT 'ORPHAN workflow.PendingTasks (deleted requests)', COUNT(*)
FROM workflow.PendingTasks x JOIN #TargetReq t ON t.RequestId = x.CorrelationId
UNION ALL
SELECT 'ORPHAN workflow.WorkflowInstances (deleted corr)', COUNT(*)
FROM workflow.WorkflowInstances x JOIN #CorrStr c ON c.Corr = x.CorrelationId
UNION ALL
SELECT 'ORPHAN appraisal.QuotationRequestAppraisals (target appr)', COUNT(*)
FROM appraisal.QuotationRequestAppraisals x JOIN #TargetAppr t ON t.AppraisalId = x.AppraisalId
UNION ALL
SELECT 'ORPHAN request.RequestTitles (deleted requests)', COUNT(*)
FROM request.RequestTitles x JOIN #TargetReq t ON t.RequestId = x.RequestId;

PRINT '=========================================================';
PRINT 'Review the counts above. If correct: COMMIT TRAN;  else: ROLLBACK TRAN;';


/*==============================================================================
  SECTION 8 - COMMIT (you decide)
  ----------------------------------------------------------------------------
  Nothing is persisted until you run ONE of these. First pass: ROLLBACK to
  validate safely; re-run and COMMIT once the numbers look right.
==============================================================================*/
-- COMMIT TRAN;
-- ROLLBACK TRAN;
