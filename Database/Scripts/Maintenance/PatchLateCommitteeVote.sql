/*==============================================================================
  PatchLateCommitteeVote.sql
  ------------------------------------------------------------------------------
  Purpose : Record, after the fact, the vote + opinion of ONE committee member who
            did not vote before the approval round resolved.

            Root cause: with VotingMode = Quorum, ApprovalActivity resolves the
            round as soon as quorum + majority are met. Members who have not voted
            yet get their PendingTask archived to workflow.CompletedTasks with
            ActionTaken = 'Closed' (ApprovalRoundClosedEventHandler) and the
            workflow moves on, so their vote can no longer be cast through the app.
            The business wants that member's vote and comment on record anyway.

  Run this MANUALLY (SSMS / sqlcmd), one member per run. It is NOT part of
  DbUp/EF migrations. Fill in the four parameters below and execute the whole file:
      sqlcmd -S <server> -d CollateralAppraisal -i PatchLateCommitteeVote.sql

  WHAT IT WRITES (all in one transaction):
    1. workflow.ApprovalVotes     INSERT the member's vote into the decisive round.
                                  VotedAt = the time this script runs (NOT backdated);
                                  SYSDATETIME() is local server time, the same clock
                                  as DateTime.Now in ApprovalVote.Create.
    2. workflow.CompletedTasks    The member's 'Closed' row becomes the vote:
                                  ActionTaken = @Vote, Remark = @Comment. CompletedAt
                                  is deliberately KEPT: the Activity Tracking Log orders
                                  and measures durations on it, and moving it to "now"
                                  would place this vote after the activities that followed
                                  the approval. The real vote time is ApprovalVotes.VotedAt.
                                  Movement is untouched: 'Closed' rows and approve/reject
                                  votes are both 'F' (only route_back maps to 'B').
    3. appraisal.AppraisalReviews TotalVotes + 1 and VotesApprove / VotesReject + 1.
                                  (No screen reads these counts - every screen counts
                                  ApprovalVotes live - but they are kept consistent.)

    Not touched: WorkflowActivityExecutions.OutputData (approveCount / totalVotes
    are history of what the engine saw when it decided; nothing reads them back)
    and the appraisal decision itself (already made; a late vote never changes it).

  MATCHING:
    Appraisal -> Appraisals.RequestId = TRY_CAST(WorkflowInstances.CorrelationId AS
    uniqueidentifier). Decisive round = the newest 'pending-approval' execution in
    ApprovalVotes (MAX(VotedAt), same rule as vw_AppraisalApprovalStatus). Roster =
    WorkflowInstances.Variables key 'pending_approval_members' (JSON array of
    {"username","role"}). The member's CompletedTask is TaskName '{activityName}:{member}',
    ActivityId 'pending-approval', CorrelationId = RequestId, ActionTaken = 'Closed',
    CompletedAt >= the round's first vote (excludes 'Closed' rows of earlier rounds).

  SAFETY:
    - Everything is validated BEFORE anything is written; any failed check THROWs
      and writes nothing.
    - Re-running for the same member is refused (the vote and the 'Closed' row are
      gone after the first run).
    - Audit columns CreatedBy / UpdatedBy are stamped 'sql-patch' so these rows are
      recognisable later.
==============================================================================*/

SET NOCOUNT ON;
-- Any runtime error aborts and rolls back instead of leaving an open transaction.
SET XACT_ABORT ON;

-- ====================== OPERATOR INPUT - fill these in ======================
DECLARE @AppraisalNumber NVARCHAR(100)  = N'';   -- e.g. N'69105532'
DECLARE @Member          NVARCHAR(255)  = N'';   -- user code, exactly as in ApprovalVotes.Member / CompletedTasks.AssignedTo
DECLARE @Vote            NVARCHAR(50)   = N'';   -- 'approve' or 'reject'
DECLARE @Comment         NVARCHAR(4000) = N'';   -- the member's opinion
-- ============================================================================

DECLARE @ActivityId          NVARCHAR(100) = N'pending-approval';
DECLARE @Operator            NVARCHAR(10)  = N'sql-patch';   -- CreatedBy/UpdatedBy are nvarchar(10)
DECLARE @AppraisalId         UNIQUEIDENTIFIER;
DECLARE @RequestId           UNIQUEIDENTIFIER;
DECLARE @WorkflowInstanceId  UNIQUEIDENTIFIER;
DECLARE @ExecutionId         UNIQUEIDENTIFIER;
DECLARE @RoundStart          DATETIME2;
DECLARE @InRoster            BIT = 0;
DECLARE @MemberRole          NVARCHAR(50);
DECLARE @Closed              TABLE (Id UNIQUEIDENTIFIER);
DECLARE @Msg                 NVARCHAR(2048);

-- 0. Input ------------------------------------------------------------------
SET @Vote = LOWER(LTRIM(RTRIM(@Vote)));
IF @Vote NOT IN (N'approve', N'reject')
    THROW 50001, N'@Vote must be ''approve'' or ''reject''.', 1;
IF ISNULL(@Member, N'') = N'' OR ISNULL(@AppraisalNumber, N'') = N''
    THROW 50002, N'@AppraisalNumber and @Member are required.', 1;

-- 1. Appraisal -> workflow instance (via CorrelationId, as vw_AppraisalApprovalStatus) --
SELECT @AppraisalId = a.Id, @RequestId = a.RequestId
FROM   appraisal.Appraisals a
WHERE  a.AppraisalNumber = @AppraisalNumber
  AND  a.IsDeleted = 0;

IF @AppraisalId IS NULL
BEGIN
    SET @Msg = CONCAT(N'Appraisal ''', @AppraisalNumber, N''' not found.');
    THROW 50010, @Msg, 1;
END;

-- 2. Decisive round = newest pending-approval execution that has votes ----------
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

-- 3. Member was on that round's roster (stored by ApprovalActivity at round start) --
SELECT @InRoster = 1, @MemberRole = m.MemberRole
FROM   workflow.WorkflowInstances wi
CROSS APPLY OPENJSON(wi.Variables, N'$.pending_approval_members')
       WITH (Username NVARCHAR(255) '$.username', MemberRole NVARCHAR(50) '$.role') m
WHERE  wi.Id = @WorkflowInstanceId
  AND  m.Username = @Member;

IF @InRoster = 0
BEGIN
    SET @Msg = CONCAT(N'''', @Member, N''' was not a member of the approval round for appraisal ''', @AppraisalNumber, N'''.');
    THROW 50030, @Msg, 1;
END;

-- 4. Member has not voted in that round ------------------------------------------
IF EXISTS (SELECT 1 FROM workflow.ApprovalVotes
           WHERE ActivityExecutionId = @ExecutionId AND Member = @Member)
BEGIN
    SET @Msg = CONCAT(N'''', @Member, N''' already has a vote in this round.');
    THROW 50040, @Msg, 1;
END;

-- 5. Exactly one 'Closed' task for the member in that round ------------------------
INSERT INTO @Closed (Id)
SELECT ct.Id
FROM   workflow.CompletedTasks ct
WHERE  ct.CorrelationId = @RequestId
  AND  ct.ActivityId    = @ActivityId
  AND  ct.ActionTaken   = N'Closed'
  AND  RIGHT(ct.TaskName, LEN(@Member) + 1) = N':' + @Member
  AND  ct.CompletedAt  >= @RoundStart;

IF (SELECT COUNT(*) FROM @Closed) <> 1
BEGIN
    SET @Msg = CONCAT(N'Expected exactly one ''Closed'' task for ''', @Member,
                      N''' in this round, found ', (SELECT COUNT(*) FROM @Closed),
                      N'. The round was closed for another reason or the vote was already patched.');
    THROW 50050, @Msg, 1;
END;

-- Write ------------------------------------------------------------------------
DECLARE @Now DATETIME2 = SYSDATETIME();

BEGIN TRANSACTION;

INSERT INTO workflow.ApprovalVotes
    (Id, AppraisalId, WorkflowInstanceId, ActivityId, ActivityExecutionId,
     Member, MemberRole, Vote, Comments, VotedAt,
     CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
VALUES
    (NEWID(), @AppraisalId, @WorkflowInstanceId, @ActivityId, @ExecutionId,
     @Member, @MemberRole, @Vote, @Comment, @Now,
     @Now, @Operator, @Now, @Operator);
PRINT CONCAT('Inserted ', @@ROWCOUNT, ' ApprovalVotes row (', @Member, ' = ', @Vote, ', role ', ISNULL(@MemberRole, N'-'), ', VotedAt ', CONVERT(VARCHAR(23), @Now, 121), ').');

UPDATE ct
SET    ct.ActionTaken = @Vote,
       ct.Remark      = @Comment,
       ct.UpdatedAt   = @Now,
       ct.UpdatedBy   = @Operator
FROM   workflow.CompletedTasks ct
INNER JOIN @Closed c ON c.Id = ct.Id;
PRINT CONCAT('Updated ', @@ROWCOUNT, ' CompletedTasks row (Closed -> ', @Vote, ').');

UPDATE appraisal.AppraisalReviews
SET    TotalVotes   = ISNULL(TotalVotes, 0) + 1,
       VotesApprove = ISNULL(VotesApprove, 0) + CASE WHEN @Vote = N'approve' THEN 1 ELSE 0 END,
       VotesReject  = ISNULL(VotesReject, 0)  + CASE WHEN @Vote = N'reject'  THEN 1 ELSE 0 END,
       UpdatedAt    = @Now,
       UpdatedBy    = @Operator
WHERE  AppraisalId = @AppraisalId;
PRINT CONCAT('Updated ', @@ROWCOUNT, ' AppraisalReviews row (TotalVotes + 1, Votes', CASE WHEN @Vote = N'approve' THEN 'Approve' ELSE 'Reject' END, ' + 1).');

COMMIT TRANSACTION;
