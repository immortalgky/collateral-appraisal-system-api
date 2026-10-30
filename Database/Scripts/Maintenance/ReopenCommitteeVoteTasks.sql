/*==============================================================================
  ReopenCommitteeVoteTasks.sql
  ------------------------------------------------------------------------------
  Purpose : Give the committee members who did not vote before the approval round
            resolved their task back, so they can vote IN THE APP and the workflow
            runs on exactly as it would have the first time.

            Root cause (same as PatchLateCommitteeVote.sql): with VotingMode = Quorum,
            ApprovalActivity resolves the round as soon as quorum + majority are met.
            The members who had not voted get their PendingTask archived to
            workflow.CompletedTasks with ActionTaken = 'Closed'
            (ApprovalRoundClosedEventHandler), the workflow runs to 'workflow-completed'
            and the appraisal becomes Completed. This script rewinds that state to
            "the round is still open, these members still owe a vote".

            Use PatchLateCommitteeVote.sql instead when the business only wants the
            vote and comment ON RECORD and does not want the case reopened.

  Run this MANUALLY (SSMS / sqlcmd -I), one appraisal per run. It is NOT part of
  DbUp/EF migrations. Fill in @AppraisalNumber below and execute the whole file:
      sqlcmd -S <server> -d CollateralAppraisal -I -i ReopenCommitteeVoteTasks.sql

  WHAT IT REWINDS (all in one transaction). Every row below is put back to the
  value it had while the round was open - values taken from rows of in-flight
  cases (Status / CompletedAt / ErrorMessage ...) and from the code that wrote them.
    1. workflow.CompletedTasks     The round's 'Closed' rows of non-voting roster members
                                   are DELETED ...
       workflow.PendingTasks       ... and re-INSERTED with the SAME Id (so the later
                                   CompletedTask.CreateFromPendingTask PK reuse does not
                                   collide). TaskStatus 'Assigned', WorkingBy / LockedAt /
                                   draft columns empty. CommitteeCode comes from the
                                   workflow variable pending_approval_committeeCode (it is
                                   not kept on CompletedTasks; vw_AppraisalApprovalStatus
                                   derives the tier from it). AssignedAt, DueAt and the SLA
                                   columns are carried over unchanged.
    2. workflow.WorkflowActivityExecutions
                                   The decisive 'pending-approval' execution goes back to
                                   InProgress (CompletedOn / CompletedBy NULL, OutputData
                                   '{}'). It MUST be this same execution: the existing
                                   rows in workflow.ApprovalVotes are keyed on its Id, so
                                   the votes already cast keep counting.
                                   The later 'workflow-completed' execution stays as
                                   history; resuming simply adds a new one.
    3. workflow.WorkflowInstances  Status Completed -> Suspended, CurrentActivityId
                                   'pending-approval', CurrentAssignee / CompletedOn NULL,
                                   ErrorMessage = the pause reason the engine writes.
                                   WorkflowEngine.ResumeWorkflowAsync validates exactly
                                   this (Suspended + CurrentActivityId = 'pending-approval').
    4. appraisal.Appraisals        Status 'UnderReview' (what SyncStatusFromWorkflow sets
                                   for 'pending-approval'), CompletedAt / ApprovedByCommittee /
                                   ActualHoursToComplete / IsWithinSLA NULL.
                                   CompletedAt IS THE KEY ONE, twice over:
                                   (a) vw_AppraisalDetail reports 'Completed' whenever
                                       CompletedAt IS NOT NULL, and the FE locks the whole
                                       appraisal read-only on a terminal status;
                                   (b) Appraisal.MarkApprovedByCommittee starts with
                                       "if (CompletedAt.HasValue) return;" - with CompletedAt
                                       still set, the re-run of AppraisalApprovedIntegrationEvent
                                       would silently do NOTHING and the appraisal would stay
                                       UnderReview after the final vote.
                                   SLAStatus is left alone: the hourly refresh rewrites it for
                                   non-closed rows and the approval handler settles it again.
    5. appraisal.AppraisalAssignments
                                   The assignment Completed by the approval goes back to
                                   'Verified' (the state MarkApprovedByCommittee finds it in),
                                   CompletedAt NULL, ProgressPercent 0. Without this a
                                   route_back vote would not rework the assignment
                                   (WorkflowTransitionedIntegrationEventHandler only looks at
                                   non-terminal assignments).
    6. request.Requests            Status 'Completed' -> 'Submitted', CompletedAt NULL (the
                                   status every in-flight request has; re-stamped by
                                   AppraisalCompletedIntegrationEvent).

  NOT TOUCHED (and why it is fine):
    - workflow.ApprovalVotes        existing votes stay and keep counting.
    - appraisal.AppraisalReviews    overwritten by the next approval (RecordCommitteeApproval).
    - workflow.WorkflowInstances.Variables  pending_approval_* / decision are overwritten by the
                                   next resolve; pending_approval_members (the roster) is what
                                   ApprovalActivity validates the voter against.
    - Collateral engagement / masters, summary document, ack queue, LOS webhook, e-mail:
      the approval event fires again on the next resolving vote. Accepted by the business;
      the Collateral upsert is idempotent per appraisal (CollateralMasterUpsertService), the
      ack consumer skips when an active queue item exists. The dashboard counter
      (common.DailyAppraisalCounts.CompletedCount) is incremented a second time.

  WHAT HAPPENS ON THE NEXT VOTE: VotingMode stays Quorum. The first vote that meets the
  rules resolves the round as usual, the other reopened members get 'Closed' again from the
  normal handler. A route_back vote behaves normally (returns the case, no re-approval).

  MATCHING (same rules as PatchLateCommitteeVote.sql):
    Appraisal -> Appraisals.RequestId = TRY_CAST(WorkflowInstances.CorrelationId AS
    uniqueidentifier). Decisive round = the newest 'pending-approval' execution in
    ApprovalVotes (MAX(VotedAt)). Roster = WorkflowInstances.Variables key
    'pending_approval_members' (JSON array of {"username","role"}). A member's Closed task =
    CompletedTasks with CorrelationId = RequestId, ActivityId 'pending-approval', ActionTaken
    'Closed', AssignedTo = member, TaskName '{activityName}:{member}', CompletedAt >= the
    round's first vote (excludes 'Closed' rows of earlier rounds).

  SAFETY:
    - Everything is validated BEFORE anything is written; any failed check THROWs and
      writes nothing. XACT_ABORT ON: a runtime error rolls the transaction back.
    - Refuses when: appraisal not found / not Completed; no decisive round; workflow not
      Completed; the decisive execution is not the newest 'pending-approval' execution or
      not Completed; anything but an End activity ran after it; a 'pending-approval'
      execution is already InProgress; any PendingTask exists for the instance; no 'Closed'
      non-voter rows in the round; a member has more than one 'Closed' row.
    - Re-running is refused (the workflow is no longer Completed).
    - Audit columns CreatedBy / UpdatedBy are stamped 'sql-reopen' so the rows are
      recognisable later (nvarchar(20) columns, 10 chars).
    - QUOTED_IDENTIFIER / ANSI_NULLS ON: Appraisals, AppraisalAssignments, Requests and
      WorkflowInstances carry filtered indexes; writing them without these fails.
==============================================================================*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
-- Any runtime error aborts and rolls back instead of leaving an open transaction.
SET XACT_ABORT ON;

-- ====================== OPERATOR INPUT - fill this in ======================
DECLARE @AppraisalNumber NVARCHAR(100) = N'';   -- e.g. N'69105532'
-- ============================================================================

DECLARE @ActivityId          NVARCHAR(100) = N'pending-approval';
DECLARE @Operator            NVARCHAR(10)  = N'sql-reopen';   -- CreatedBy/UpdatedBy are nvarchar(20)
DECLARE @PauseReason         NVARCHAR(4000) = N'Activity pending-approval requires external completion'; -- WorkflowEngine, Pending branch
DECLARE @AppraisalId         UNIQUEIDENTIFIER;
DECLARE @RequestId           UNIQUEIDENTIFIER;
DECLARE @AppraisalStatus     NVARCHAR(450);
DECLARE @WorkflowInstanceId  UNIQUEIDENTIFIER;
DECLARE @WorkflowStatus      NVARCHAR(450);
DECLARE @ExecutionId         UNIQUEIDENTIFIER;
DECLARE @ExecutionStatus     NVARCHAR(450);
DECLARE @ExecutionStartedOn  DATETIME2;
DECLARE @RoundStart          DATETIME2;
DECLARE @Variables           NVARCHAR(MAX);
DECLARE @CommitteeCode       NVARCHAR(100);
DECLARE @Roster              TABLE (Username NVARCHAR(255) NOT NULL);
DECLARE @Closed              TABLE (Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, Member NVARCHAR(255) NOT NULL);
DECLARE @AssignmentId        UNIQUEIDENTIFIER;
DECLARE @Members            NVARCHAR(MAX);
DECLARE @Count               INT;
DECLARE @Msg                 NVARCHAR(2048);

-- 0. Input ------------------------------------------------------------------
SET @AppraisalNumber = LTRIM(RTRIM(@AppraisalNumber));
IF ISNULL(@AppraisalNumber, N'') = N''
    THROW 50001, N'@AppraisalNumber is required.', 1;

-- 1. Appraisal: found and Completed -----------------------------------------------
SELECT @AppraisalId = a.Id, @RequestId = a.RequestId, @AppraisalStatus = a.Status
FROM   appraisal.Appraisals a
WHERE  a.AppraisalNumber = @AppraisalNumber
  AND  a.IsDeleted = 0;

IF @AppraisalId IS NULL
BEGIN
    SET @Msg = CONCAT(N'Appraisal ''', @AppraisalNumber, N''' not found.');
    THROW 50010, @Msg, 1;
END;

IF @AppraisalStatus <> N'Completed'
BEGIN
    SET @Msg = CONCAT(N'Appraisal ''', @AppraisalNumber, N''' is ''', @AppraisalStatus,
                      N''', not Completed. Only an appraisal completed by its committee round can be reopened.');
    THROW 50011, @Msg, 1;
END;

-- 2. Decisive round = newest pending-approval execution that has votes -----------------
SELECT TOP (1)
       @WorkflowInstanceId = av.WorkflowInstanceId,
       @ExecutionId        = av.ActivityExecutionId,
       @RoundStart         = MIN(av.VotedAt)
FROM   workflow.ApprovalVotes av
INNER JOIN workflow.WorkflowInstances wi ON wi.Id = av.WorkflowInstanceId
WHERE  av.ActivityId = @ActivityId
  AND  TRY_CAST(wi.CorrelationId AS uniqueidentifier) = @RequestId
GROUP BY av.WorkflowInstanceId, av.ActivityExecutionId
ORDER BY MAX(av.VotedAt) DESC;

IF @ExecutionId IS NULL
BEGIN
    SET @Msg = CONCAT(N'No ''', @ActivityId, N''' approval round with votes found for appraisal ''', @AppraisalNumber, N'''.');
    THROW 50020, @Msg, 1;
END;

-- 3. Workflow state: Completed, decisive execution is the newest pending-approval one ----
SELECT @WorkflowStatus = wi.Status, @Variables = wi.Variables
FROM   workflow.WorkflowInstances wi
WHERE  wi.Id = @WorkflowInstanceId;

IF @WorkflowStatus <> N'Completed'
BEGIN
    SET @Msg = CONCAT(N'Workflow is ''', @WorkflowStatus, N''', not Completed. Nothing to reopen.');
    THROW 50021, @Msg, 1;
END;

SELECT @ExecutionStatus = e.Status, @ExecutionStartedOn = e.StartedOn
FROM   workflow.WorkflowActivityExecutions e
WHERE  e.Id = @ExecutionId;

IF @ExecutionStatus <> N'Completed'
    THROW 50022, N'The decisive ''pending-approval'' execution is not Completed.', 1;

IF EXISTS (SELECT 1 FROM workflow.WorkflowActivityExecutions e
           WHERE e.WorkflowInstanceId = @WorkflowInstanceId AND e.ActivityId = @ActivityId
             AND e.Id <> @ExecutionId AND e.StartedOn > @ExecutionStartedOn)
    THROW 50023, N'A newer ''pending-approval'' execution exists than the one that holds the votes.', 1;

-- Only the End activity may have run after the approval; anything else means the case moved on.
IF EXISTS (SELECT 1 FROM workflow.WorkflowActivityExecutions e
           WHERE e.WorkflowInstanceId = @WorkflowInstanceId AND e.Id <> @ExecutionId
             AND e.StartedOn > @ExecutionStartedOn AND e.ActivityType <> N'EndActivity')
    THROW 50024, N'An activity other than the End activity ran after the approval round. The workflow cannot be rewound safely.', 1;

IF EXISTS (SELECT 1 FROM workflow.WorkflowActivityExecutions e
           WHERE e.WorkflowInstanceId = @WorkflowInstanceId AND e.ActivityId = @ActivityId
             AND e.Status = N'InProgress')
    THROW 50025, N'A ''pending-approval'' execution is already InProgress.', 1;

IF EXISTS (SELECT 1 FROM workflow.PendingTasks pt WHERE pt.WorkflowInstanceId = @WorkflowInstanceId)
    THROW 50026, N'The workflow instance still has PendingTasks. Refusing to add more.', 1;

-- 4. Roster and the 'Closed' non-voters of that round --------------------------------
INSERT INTO @Roster (Username)
SELECT m.Username
FROM   OPENJSON(@Variables, N'$.pending_approval_members')
       WITH (Username NVARCHAR(255) '$.username') m
WHERE  m.Username IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM @Roster)
    THROW 50030, N'The workflow has no pending_approval_members roster variable.', 1;

INSERT INTO @Closed (Id, Member)
SELECT ct.Id, r.Username
FROM   workflow.CompletedTasks ct
INNER JOIN @Roster r ON r.Username = ct.AssignedTo
WHERE  ct.CorrelationId = @RequestId
  AND  ct.ActivityId    = @ActivityId
  AND  ct.ActionTaken   = N'Closed'
  AND  RIGHT(ct.TaskName, LEN(r.Username) + 1) = N':' + r.Username
  AND  ct.CompletedAt  >= @RoundStart
  AND  NOT EXISTS (SELECT 1 FROM workflow.ApprovalVotes av
                   WHERE av.ActivityExecutionId = @ExecutionId AND av.Member = r.Username);

IF NOT EXISTS (SELECT 1 FROM @Closed)
BEGIN
    SET @Msg = CONCAT(N'No ''Closed'' task of a non-voting roster member found in the decisive round for appraisal ''',
                      @AppraisalNumber, N'''. Everyone voted, or the round was closed for another reason.');
    THROW 50050, @Msg, 1;
END;

IF EXISTS (SELECT 1 FROM @Closed GROUP BY Member HAVING COUNT(*) > 1)
    THROW 50051, N'A member has more than one ''Closed'' task in this round. Refusing to guess which one to reopen.', 1;

SET @CommitteeCode = NULLIF(JSON_VALUE(@Variables, N'$.pending_approval_committeeCode'), N'');
SELECT @Members = STRING_AGG(Member, N', ') WITHIN GROUP (ORDER BY Member) FROM @Closed;

-- Write ------------------------------------------------------------------------
DECLARE @Now DATETIME2 = SYSDATETIME();   -- local server time, the clock DateTime.Now uses

BEGIN TRANSACTION;

-- 1. Closed CompletedTasks -> PendingTasks, same Id
INSERT INTO workflow.PendingTasks
    (Id, CorrelationId, TaskName, TaskDescription, TaskStatus, AssignedTo, AssignedType,
     AssignedAt, AssigneeAssignedAt, WorkingBy, LockedAt, OpenedAt,
     WorkflowInstanceId, ActivityId, DueAt, SlaStartAt, SlaStatus, SlaBreachedAt, SlaDurationHours,
     Movement, AssigneeCompanyId, CommitteeCode,
     DecisionTaken, Comment, ReasonCode, DraftAssignee,
     CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT ct.Id, ct.CorrelationId, ct.TaskName, ct.TaskDescription, N'Assigned', ct.AssignedTo, ct.AssignedType,
       ct.AssignedAt, ct.AssigneeAssignedAt, NULL, NULL, ct.OpenedAt,
       @WorkflowInstanceId, ct.ActivityId, ct.DueAt, ct.SlaStartAt, ct.SlaStatus, ct.SlaBreachedAt, ct.SlaDurationHours,
       ct.Movement, ct.AssigneeCompanyId, @CommitteeCode,
       NULL, NULL, NULL, NULL,
       @Now, @Operator, @Now, @Operator
FROM   workflow.CompletedTasks ct
INNER JOIN @Closed c ON c.Id = ct.Id;
SET @Count = @@ROWCOUNT;
PRINT CONCAT('Inserted ', @Count, ' PendingTasks row(s) (members: ', @Members, ').');
IF @Count <> (SELECT COUNT(*) FROM @Closed)
    THROW 50090, N'PendingTasks insert count differs from the Closed rows found.', 1;

DELETE ct
FROM   workflow.CompletedTasks ct
INNER JOIN @Closed c ON c.Id = ct.Id;
IF @@ROWCOUNT <> @Count
    THROW 50091, N'CompletedTasks delete count differs from the Closed rows found.', 1;
PRINT CONCAT('Deleted ', @Count, ' CompletedTasks row(s) (the ''Closed'' rows).');

-- 2. Decisive execution back to InProgress
UPDATE workflow.WorkflowActivityExecutions
SET    Status      = N'InProgress',
       CompletedOn = NULL,
       CompletedBy = NULL,
       OutputData  = N'{}',
       Comments    = NULL,
       ErrorMessage = NULL,
       UpdatedAt   = @Now,
       UpdatedBy   = @Operator
WHERE  Id = @ExecutionId AND Status = N'Completed';
IF @@ROWCOUNT <> 1 THROW 50092, N'Could not rewind the decisive execution.', 1;
PRINT 'Rewound the decisive pending-approval execution to InProgress.';

-- 3. Workflow instance Completed -> Suspended at pending-approval
UPDATE workflow.WorkflowInstances
SET    Status            = N'Suspended',
       CurrentActivityId = @ActivityId,
       CurrentAssignee   = NULL,
       CompletedOn       = NULL,
       ErrorMessage      = @PauseReason,
       UpdatedAt         = @Now,
       UpdatedBy         = @Operator
WHERE  Id = @WorkflowInstanceId AND Status = N'Completed';
IF @@ROWCOUNT <> 1 THROW 50093, N'Could not rewind the workflow instance.', 1;
PRINT 'Rewound the workflow instance to Suspended at pending-approval.';

-- 4. Appraisal back to UnderReview, approval stamps cleared
UPDATE appraisal.Appraisals
SET    Status               = N'UnderReview',
       CompletedAt          = NULL,
       ApprovedByCommittee  = NULL,
       ActualHoursToComplete = NULL,
       IsWithinSLA          = NULL,
       UpdatedAt            = @Now,
       UpdatedBy            = @Operator
WHERE  Id = @AppraisalId AND Status = N'Completed';
IF @@ROWCOUNT <> 1 THROW 50094, N'Could not rewind the appraisal.', 1;
PRINT 'Rewound the appraisal to UnderReview (CompletedAt / ApprovedByCommittee cleared).';

-- 5. The assignment the approval Completed goes back to Verified (newest Completed one)
SELECT TOP (1) @AssignmentId = aa.Id
FROM   appraisal.AppraisalAssignments aa
WHERE  aa.AppraisalId = @AppraisalId AND aa.AssignmentStatus = N'Completed'
ORDER BY aa.CompletedAt DESC, aa.AssignedAt DESC;

IF @AssignmentId IS NULL
    PRINT 'WARNING: no Completed assignment found; assignment not touched.';
ELSE
BEGIN
    UPDATE appraisal.AppraisalAssignments
    SET    AssignmentStatus = N'Verified',
           CompletedAt      = NULL,
           ProgressPercent  = 0,
           UpdatedAt        = @Now,
           UpdatedBy        = @Operator
    WHERE  Id = @AssignmentId AND AssignmentStatus = N'Completed';
    IF @@ROWCOUNT <> 1 THROW 50095, N'Could not rewind the assignment.', 1;
    PRINT 'Rewound the assignment to Verified.';
END;

-- 6. Request back to Submitted (re-stamped Completed by AppraisalCompletedIntegrationEvent)
UPDATE request.Requests
SET    Status      = N'Submitted',
       CompletedAt = NULL,
       UpdatedAt   = @Now,
       UpdatedBy   = @Operator
WHERE  Id = @RequestId AND Status = N'Completed';
PRINT CONCAT('Updated ', @@ROWCOUNT, ' Requests row(s) (Completed -> Submitted).');

COMMIT TRANSACTION;

PRINT CONCAT('Reopened the committee round of appraisal ''', @AppraisalNumber, ''' for: ', @Members, '.');
