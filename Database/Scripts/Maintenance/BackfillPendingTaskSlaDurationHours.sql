/*==============================================================================
  BackfillPendingTaskSlaDurationHours.sql
  ------------------------------------------------------------------------------
  Purpose : Populate workflow.PendingTasks.SlaDurationHours for tasks that were
            already pending when the column was added. New tasks self-populate
            at assignment time (SlaCalculator -> TaskAssignedEvent -> PendingTask);
            this one-time script fills the historical rows so the task list can
            show the SLA policy budget (e.g. "48h") alongside the due date.

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations.

  WHY pure SQL (no calculation):
    SlaDurationHours is a stored value on workflow.SlaPolicies (DurationHours) —
    reading it needs NO business-time math (that only ever produced DueAt). The
    only logic is:
      (a) resolve which Activity-scope policy applies (priority + specificity), and
      (b) let a governing Stage window override it (window budget beats per-activity).
    Both mirror SlaCalculator.CalculateActivityDueAtAsync /
    ResolveGoverningStageDueAtAsync for the DEFAULT seeded configuration, so the
    backfilled value matches what a fresh assignment stamps for those tasks.

  KNOWN GAPS vs the C# resolver (a pure-SQL backfill reading only SlaPolicies
  cannot reproduce these; the affected rows are left NULL and self-heal on the
  task's next assignment — the display chip is simply hidden until then):
    1. JSON timeoutDuration fallback: CalculateActivityDueAtAsync step 2 falls
       back to the workflow-definition JSON `timeoutDuration` when NO Activity
       policy row matches (e.g. a pending-approval task). That budget lives in
       WorkflowDefinitions.JsonDefinition, not SlaPolicies, so it is not
       backfilled here.
    2. Loan/appraisal-type-specific policies: the resolver matches
       (LoanType IS NULL OR = task.loanType). The task's loanType/appraisalType
       are not stored on PendingTasks (only in workflow Variables), so this
       script matches only the loan/appraisal-agnostic (wildcard) policy. Under
       the current seed all policies are wildcard on these dimensions, so there
       is no divergence today; revisit if segment-specific policies are added.

  STAGE-SPAN CAVEAT:
    Membership of a Stage window is expanded here purely from the policy columns
    (StartActivityKey / EndActivityKey / OPENJSON(MiddleActivityKeys)). Every
    SEEDED stage policy carries its members explicitly (ext window lists
    MiddleActivityKeys; int window is single-activity start=end), so no
    workflow-JSON graph walk is needed. If a future Stage policy has
    MiddleActivityKeys IS NULL AND StartActivityKey <> EndActivityKey, its span
    cannot be resolved by this script and it must be revisited (the C# resolver
    would graph-walk that case).

  IDEMPOTENT: only touches rows where SlaDurationHours IS NULL (and a policy
    resolves). There is intentionally NO DueAt predicate — an appointment-anchored
    task can have a known budget while its DueAt is still deferred (see the
    `targets` CTE). Rows with no resolvable policy are left NULL by the final
    WHERE. Re-running affects 0 rows.
==============================================================================*/

SET NOCOUNT ON;
-- Ensure any runtime error aborts and rolls back the transaction rather than leaving it
-- open (holding locks on live workflow.PendingTasks) for an operator to clean up by hand.
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Expand each Stage-scope policy to the set of activity IDs it governs.
-- Members = StartActivityKey UNION EndActivityKey UNION OPENJSON(MiddleActivityKeys).
;WITH stage_members AS (
    SELECT sp.DurationHours,
           sp.Priority,
           sp.WorkflowDefinitionId,
           sp.CompanyId,
           sp.LoanType,
           sp.AppraisalType,
           m.ActivityId
    FROM   workflow.SlaPolicies sp
    CROSS APPLY (
        SELECT sp.StartActivityKey AS ActivityId WHERE sp.StartActivityKey IS NOT NULL
        UNION
        SELECT sp.EndActivityKey            WHERE sp.EndActivityKey   IS NOT NULL
        UNION
        -- Guard with ISJSON so an empty-string / whitespace / malformed value can't raise
        -- OPENJSON error 13609 and abort the whole transaction (the C# resolver likewise
        -- swallows a JsonException and falls through to just start+end).
        SELECT value FROM OPENJSON(CASE WHEN ISJSON(sp.MiddleActivityKeys) = 1
                                        THEN sp.MiddleActivityKeys ELSE N'[]' END)
    ) m
    WHERE sp.Scope = 2                 -- Stage
      AND m.ActivityId IS NOT NULL
),
targets AS (
    SELECT pt.Id,
           pt.ActivityId,
           pt.AssigneeCompanyId,
           wi.WorkflowDefinitionId
    FROM   workflow.PendingTasks pt
    LEFT JOIN workflow.WorkflowInstances wi ON wi.Id = pt.WorkflowInstanceId
    -- Target every un-backfilled task, NOT only those with a DueAt: an appointment-anchored
    -- (window or per-activity) task can have a known budget while its DueAt is still deferred.
    -- Rows with no resolvable policy are skipped by the final WHERE (COALESCE ... IS NOT NULL).
    WHERE  pt.SlaDurationHours IS NULL
)
UPDATE pt
SET    pt.SlaDurationHours = COALESCE(stage.DurationHours, act.DurationHours)
FROM   workflow.PendingTasks pt
JOIN   targets t ON t.Id = pt.Id
-- (a) Governing Stage window, if this activity is a member of one. Wins over per-activity.
OUTER APPLY (
    SELECT TOP 1 sm.DurationHours
    FROM   stage_members sm
    WHERE  sm.ActivityId = t.ActivityId
      AND  (sm.WorkflowDefinitionId IS NULL OR sm.WorkflowDefinitionId = t.WorkflowDefinitionId)
      AND  (sm.CompanyId IS NULL OR sm.CompanyId = t.AssigneeCompanyId)
      AND  sm.LoanType IS NULL
      AND  sm.AppraisalType IS NULL
    ORDER BY sm.Priority,
             CASE WHEN sm.CompanyId IS NOT NULL THEN 0 ELSE 1 END
) stage
-- (b) Per-activity policy (default fallback). Specific ActivityId beats the "*" wildcard.
OUTER APPLY (
    SELECT TOP 1 sp.DurationHours
    FROM   workflow.SlaPolicies sp
    WHERE  sp.Scope = 1               -- Activity
      AND  (sp.ActivityId = t.ActivityId OR sp.ActivityId = N'*')
      AND  (sp.WorkflowDefinitionId IS NULL OR sp.WorkflowDefinitionId = t.WorkflowDefinitionId)
      AND  (sp.CompanyId IS NULL OR sp.CompanyId = t.AssigneeCompanyId)
      AND  sp.LoanType IS NULL
      AND  sp.AppraisalType IS NULL
    ORDER BY sp.Priority,
             CASE WHEN sp.ActivityId = t.ActivityId THEN 0 ELSE 1 END
) act
WHERE  COALESCE(stage.DurationHours, act.DurationHours) IS NOT NULL;

PRINT CONCAT('Backfilled SlaDurationHours on ', @@ROWCOUNT, ' pending task(s).');

COMMIT TRANSACTION;
