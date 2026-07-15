/*==============================================================================
  BackfillPendingTaskOpenedAt.sql
  ------------------------------------------------------------------------------
  Purpose : Populate workflow.PendingTasks.OpenedAt for tasks that were already
            opened (InProgress) before the column was added. New opens self-
            populate (OpenTask/StartTask -> PendingTask.StartWorking stamps
            OpenedAt = DateTime.Now on the first InProgress transition); this
            one-time script fills the historical rows so the Monitoring "Open
            Date" column shows a value instead of "—".

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations.

  DEFAULT VALUE:
    We never recorded when these historical tasks were actually opened, so there
    is no true source. Per request, OpenedAt is defaulted to AssignedAt — the
    earliest defensible lower bound (a task can only be opened at or after it was
    assigned). Treat these backfilled values as approximate.

  SCOPE — only OPENED tasks:
    Only tasks currently InProgress have been opened. Tasks still in 'Assigned'
    have NOT been opened yet and are intentionally left NULL so they stamp a real
    OpenedAt the first time a user opens them.

  IDEMPOTENT: only touches rows where OpenedAt IS NULL and TaskStatus =
    'InProgress'. Re-running affects 0 rows.
==============================================================================*/

SET NOCOUNT ON;
-- Ensure any runtime error aborts and rolls back the transaction rather than leaving it
-- open (holding locks on live workflow.PendingTasks) for an operator to clean up by hand.
SET XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE workflow.PendingTasks
SET    OpenedAt = AssignedAt
WHERE  OpenedAt IS NULL
  AND  TaskStatus = 'InProgress';

PRINT CONCAT('Backfilled OpenedAt on ', @@ROWCOUNT, ' opened pending task(s).');

COMMIT TRANSACTION;
