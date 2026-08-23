/*
  20260821140000_Backfill_ClearStalePendingTaskOpenedAt.sql

  Purpose : Clear workflow.PendingTasks.OpenedAt values that belong to a PREVIOUS holder.

  Why     : OpenedAt is stamped with `??=` (once, never overwritten) and, until the holder-clock
            work landed, PendingTask.Reassign() did not clear it on hand-off. A task redirected by
            a supervisor therefore carried the original assignee's open time into the new holder's
            row, and because of the `??=` the new holder opening the task could never correct it —
            the history tooltip reported an open time from before they were even involved.

  Rule    : OpenedAt < AssigneeAssignedAt is impossible by definition — nobody opens a task before
            receiving it. Every such row is a leftover, so NULL it out. NULL reads as "not opened
            yet", which is honest; StartWorking() now re-stamps on the next open (it treats a stamp
            older than AssigneeAssignedAt as stale), so these rows self-correct from here.

  Scope   : Only rows that violate the invariant. Rows never reassigned are untouched, since their
            OpenedAt is by construction >= AssigneeAssignedAt.

  IDEMPOTENT: re-running matches nothing once the rows are cleared.
*/

UPDATE workflow.PendingTasks
SET    OpenedAt = NULL
WHERE  OpenedAt IS NOT NULL
  AND  OpenedAt < AssigneeAssignedAt;
