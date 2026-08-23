/*
  20260821140000_Backfill_ClearStalePendingTaskOpenedAt.sql

  Purpose : Clear workflow.PendingTasks.OpenedAt values that belong to a PREVIOUS holder.

  Why     : OpenedAt is stamped with `??=` (once, never overwritten) and, until the holder-clock
            work landed, PendingTask.Reassign() did not clear it on hand-off. A task redirected by
            a supervisor therefore carried the original assignee's open time into the new holder's
            row, and because of the `??=` the new holder opening the task could never correct it —
            the history tooltip reported an open time from before they were even involved.

  Rule    : Clear OpenedAt only where BOTH hold: a supervisor actually handed the task off (the
            audit row ReassignTaskCommandHandler writes — ActionTaken='Reassigned', matching
            CorrelationId + ActivityId and the same frozen AssignedAt), AND the stamp was made at
            or before that hand-off, so it belongs to the outgoing holder. NULL reads as "not
            opened yet"; StartWorking() re-stamps on the next open.

  NOTE    : On a host whose local time IS the configured application timezone — the intended
            deployment — the simpler `OpenedAt < AssigneeAssignedAt` test is a genuine invariant and
            would be correct. The predicate below is hardening, not a fix for a live defect. It is
            written this way because the application itself already refuses to trust host time
            (ApplicationNow exists precisely to bypass it), and a one-time UPDATE that silently and
            irreversibly discards data should not rest on the opposite assumption.

  WHY NOT `OpenedAt < AssigneeAssignedAt` alone: the two columns were written by DIFFERENT clocks
            before the fix. OpenedAt used DateTime.Now (the HOST's local time) while
            AssignedAt/AssigneeAssignedAt come from IDateTimeProvider.ApplicationNow, which resolves
            the configured application timezone (Asia/Bangkok, ForceUtc=false). On a host running
            UTC the two sit seven hours apart, so nearly every legitimately-opened task satisfies
            that test. Measured on dev by simulating the skew: 69 rows would match, 1 was real.

  WHY NOT the audit row alone: a task handed off BEFORE this script runs may since have been opened
            by its new holder. That stamp is correct and must survive, which is what the
            `pt.OpenedAt <= ct.CompletedAt` comparison protects — ct.CompletedAt is the hand-off
            instant, so only stamps predating it are the outgoing holder's.

  RESIDUAL : On a skewed host the comparison can still over-match a row opened by its new holder
            within the offset window after the hand-off. That is bounded to tasks that genuinely
            changed hands, and the cost is a NULL that self-corrects the next time the holder opens
            the task — unlike a bulk clear of rows that never changed hands at all.

  IDEMPOTENT: re-running matches nothing once the rows are cleared.
*/

UPDATE pt
SET    pt.OpenedAt = NULL
FROM   workflow.PendingTasks pt
WHERE  pt.OpenedAt IS NOT NULL
  AND  EXISTS (SELECT 1
               FROM   workflow.CompletedTasks ct
               WHERE  ct.CorrelationId = pt.CorrelationId
                 AND  ct.ActivityId    = pt.ActivityId
                 AND  ct.ActionTaken   = 'Reassigned'
                 AND  ct.AssignedAt    = pt.AssignedAt
                 AND  pt.OpenedAt      <= ct.CompletedAt);
