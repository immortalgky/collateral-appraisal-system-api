/*==============================================================================
  BackfillCompletedTaskCommitteeRemark.sql
  ------------------------------------------------------------------------------
  Purpose : Populate workflow.CompletedTasks.Remark for historical "Committee
            Approval" rows whose comment was entered by the voter but never
            written to Remark (so the Activity Tracking Log REMARK column shows
            "—" instead of the comment icon).

            Root cause: ApprovalActivity published each per-member
            TaskCompletedDomainEvent with a NULL Remark even though the voter's
            comment was in scope. The comment WAS still persisted on
            workflow.ApprovalVotes.Comments, so this script recovers it from
            there. (The forward fix passes the comment through, so rows completed
            after that deploy self-populate and are not touched here.)

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations.

  MATCHING:
    A committee CompletedTask row is TaskName = '{activityName}:{voter}',
    CorrelationId = TRY_CAST(WorkflowInstances.CorrelationId AS uniqueidentifier),
    ActivityId, and ActionTaken = the vote. We join to the ApprovalVotes row for
    the same instance + activity + voter (parsed from TaskName) + vote. When a
    voter voted across multiple rounds (e.g. after a route-back), we take the
    vote whose VotedAt is closest to the task's CompletedAt.

  SCOPE / SAFETY:
    - Only touches rows where Remark IS NULL (never overwrites an existing
      remark), so it is IDEMPOTENT — re-running affects 0 additional rows.
    - Only fills from votes that actually have a non-empty comment.
    - Legacy votes (WorkflowInstanceId IS NULL) have no CompletedTask row and are
      naturally excluded.
==============================================================================*/

SET NOCOUNT ON;
-- Ensure any runtime error aborts and rolls back rather than leaving an open
-- transaction holding locks on live workflow.CompletedTasks.
SET XACT_ABORT ON;

BEGIN TRANSACTION;

WITH VoteMatch AS (
    SELECT
        CT.Id       AS CompletedTaskId,
        AV.Comments AS Comments,
        ROW_NUMBER() OVER (
            PARTITION BY CT.Id
            -- Closest vote in time wins; VotedAt DESC breaks exact ties.
            ORDER BY ABS(DATEDIFF(SECOND, AV.VotedAt, CT.CompletedAt)) ASC,
                     AV.VotedAt DESC
        ) AS rn
    FROM workflow.CompletedTasks CT
    INNER JOIN workflow.WorkflowInstances WI
        ON TRY_CAST(WI.CorrelationId AS uniqueidentifier) = CT.CorrelationId
    INNER JOIN workflow.ApprovalVotes AV
        ON  AV.WorkflowInstanceId = WI.Id
        AND AV.ActivityId         = CT.ActivityId
        AND AV.Vote               = CT.ActionTaken
        -- Voter = the segment after the LAST ':' in TaskName ('{activity}:{voter}').
        AND AV.Member = RIGHT(CT.TaskName, CHARINDEX(':', REVERSE(CT.TaskName)) - 1)
    WHERE CT.Remark IS NULL
      AND CHARINDEX(':', CT.TaskName) > 0          -- guard RIGHT()/CHARINDEX math
      AND AV.Comments IS NOT NULL
      AND LEN(AV.Comments) > 0
)
UPDATE CT
SET    CT.Remark = VM.Comments
FROM   workflow.CompletedTasks CT
INNER JOIN VoteMatch VM
        ON VM.CompletedTaskId = CT.Id
       AND VM.rn = 1;

PRINT CONCAT('Backfilled Remark on ', @@ROWCOUNT, ' committee CompletedTask row(s).');

COMMIT TRANSACTION;
