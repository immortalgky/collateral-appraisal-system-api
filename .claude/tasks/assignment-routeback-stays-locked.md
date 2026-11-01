# Route-back to admin keeps the assignment locked

Branch: `immortalgky/fix-assignment-status-pending` (BE + FE twin). Target: `main` only.

## Problem
PR #340 (2026-07-29) reset `AppraisalAssignment.AssignmentStatus` to Pending whenever the workflow
came back to `appraisal-assignment`. That unlocked the Administration page (FE locks on non-Pending),
the quotation eligibility list and the fee admin stage, so a returned case could be dispatched again.
Business rule (user, 2026-11-01): a case returned to admin stays locked; the admin forwards it from
the Decision Summary page (P / C / R).

## Todo
- [x] Remove the generic reset in `WorkflowTransitionedIntegrationEventHandler` (case `appraisal-assignment`)
- [x] Remove the routed-back guard in `SetOfflineExternalEngagementCommandHandler` (user: not restored)
- [x] Fix `AutoAssignClassifier` comment + message
- [x] Exception: company declined at `ext-appraisal-assignment` → reset to Pending (admin re-picks)
- [x] Block StartQuotationFromTask on a non-Pending current row
- [x] FE: `assignmentNotReset` i18n text (en/th/zh) → Decision Summary
- [x] Round-2 fixes: InProgress decline, same election as AssignAppraisal, guard AddAppraisalToDraft/CreateQuotation, pass-through tests
- [x] Review loop until no new critical/high/medium
- [x] test-automator gates (BE build+unit tests; FE tsc + vitest, check datePopover baseline)
- [ ] User tests, then PRs (BE + FE)

## Settled decisions (do not re-raise)
- Route-backs from `int-appraisal-execution` / `int-offline-book-keyin` keep the row Assigned/InProgress by design; admin cannot re-assign, only P/C/R from Decision Summary.
- No data fix for rows already reset by the old code (45 locally); user decision.
- SetOfflineExternalEngagement routed-back guard stays deleted; low risk on legacy rows accepted by user.
- SLA stamping at `appraisal-assignment`: unreachable, no Stage policy starts there.
- No test file exists for `WorkflowTransitionedIntegrationEventHandler`; none added.
- ExternalEngagementCycle left open after a company decline: pre-existing, out of scope.
- `AssignmentNotReset` enum / wire value `assignmentNotReset` kept unchanged.
- UI-entry company route_back (ext-appraisal-assignment → appraisal-initiation-check → … → appraisal-assignment) stays locked: it is "ask for more info", the same company continues via P.
- Stale/redelivered decline event resetting a re-assigned row: rare race, accepted (old code had the same).
- Message wording during the async reset gap (classifier/StartQuotation): rare race, accepted.
- Company cancels at ext-appraisal-assignment before the CompanyAssigned consumer runs → row ends Assigned, no reset: rare race (human action vs ms consumer), old code same; accepted.
- Decline reset keeps SLADueDate / StartedAt / QuotationRequestId of the declined company: pre-existing (old reset did the same) → separate ticket, not this PR.
- Guard error precedence differs between StartFromTask / AddToDraft / CreateQuotation: nit, accepted.
- QuotationAutoAssignStateReader picks "Pending first" and keeps Rejected/Cancelled, unlike the election: pre-existing reader → separate ticket.
- Guard lets an appraisal with no live assignment row through (AssignAppraisal would refuse it later): anomalous legacy state, no guard existed before; accepted.
- Guard check-then-act without a row lock (two admins within ms): rare race, accepted.

## Review loop
- R1 whole branch: 9 → 3 settled by user (company-decline unlock, quotation block + FE text, guard not restored), rest declined.
- R2 fix diff: 7 → 4 fixed (InProgress decline, election, AddToDraft/CreateQuotation guard, tests), 3 declined.
- R3 fix diff: 9 → guard as app service + narrow read + message + tests; rest declined.
- R4 fix diff: 5 → no in-scope bugs; 2 test gaps added; stop (no new critical/high/medium).

## Review
- Route-back to `appraisal-assignment` no longer resets the row, except a company decline/return from
  `ext-appraisal-assignment` (Assigned|InProgress → Pending via `ReturnToAssignmentQueue`).
- New `QuotationAssignmentGuard` (scoped app service) refuses StartQuotationFromTask / AddAppraisalToDraft /
  CreateQuotation when the current row (same election as AssignAppraisal) is not Pending.
- `AutoAssignClassifier` comment/message and FE `assignmentNotReset` text (en/th/zh) point to Decision Summary.
- Gates: BE build 0 errors; Appraisal.Tests 1548/1548; Workflow.Tests 1219 pass, 1 pre-skipped.
  FE tsc 0 errors; vitest 4814 pass, 1 fail `datePopover.test.tsx` (src/shared untouched, deterministic, judged pre-existing).
- New warnings: 6× xUnit1051 (CancellationToken) in the new test files.
- Follow-up tickets (not this PR): decline reset keeps old SLADueDate/StartedAt/QuotationRequestId;
  QuotationAutoAssignStateReader election differs from Current().
- PR review (Copilot): company-selection no_match also resets — failed selection must let the admin re-pick.
