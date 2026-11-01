# #622 Assignment email: in-system external (EXT) and system-assigned cases

Branch `immortalgky/implement-622-ext-auto-assign-email` (worktree `halibut`), off the local integration branch
`feat/617-admin-company-email` @ 4b39e989 (#620, #621, #623, #624 merged). Parent spec #617.

## Flow
1. `CompanyAssignedIntegrationEventHandler`: when the row was Pending (a genuine (re)assignment, the same marker as
   EXTO's `wasPending`) and the assignment has no Pending/NotSent email row (nobody pressed มอบหมาย: round-robin,
   quotation auto-assign, CI forced, after PMA) → add a Pending row built from the template, in the same save.
   Company comes from the event, the fee question is explicit (external, quotation|manual, company), ผู้รับผิดชอบ
   stays `{{responsible}}`.
2. `InternalFollowupAssignedIntegrationEventHandler`: newest Pending/NotSent row of the assignment:
   NotSent → nothing; Pending → `AssignmentEmailDispatcher.QueueAsync` (responsible = follow-up officer, Cc chips
   expanded for that officer); none → nothing (route-back replay: CompanyAssigned did not find the row Pending, or
   round-robin replay published no CompanyAssigned at all).
3. `InternalFollowupSelectionActivity` no_match publishes the event with no officer. The handler then leaves the
   row's follow-up fields alone, fills ผู้รับผิดชอบ with "-" and the follow-up Cc chips resolve to nobody.

## Todo
- [x] Activity: no_match publishes the event (officer null)
- [x] Event: officer / method nullable
- [x] Composer: EXT handover template (company + fee question explicit, responsible open)
- [x] CompanyAssigned handler: template row on a Pending row with no admin choice
- [x] Followup handler: queue the Pending row; "-" on no_match
- [x] Tests: activity no_match; seam EXT manual / RoundRobin / Quotation / Forced; replay; NotSent; no_match
- [x] Matt code-review: Standards no hard violation; fixed quotation literal → constant, shared FromTemplate,
      named Handover args, nullable follow-up method (no "" sentinel). Spec: fixed new company on a row left Assigned
      (automatic route-back through initial-routing) → template sent
- [ ] /code-review loop, gates (test-automator)
- [ ] User test → merge `--no-ff` into the integration branch, close #622 by hand

## Settled decisions (user, 2026-11-01)
- New-vs-replay is decided in CompanyAssigned (row was Pending, **or the event's company differs from the one the row
  holds** — user, from Matt review), carried to the follow-up by the Pending email row.
  No migration. A → cancel → quotation → A wins again counts as a new engagement and sends.
- no_match: ผู้รับผิดชอบ = "-".
- Ordering: CompanyAssigned is staged before InternalFollowupAssigned in the same workflow transaction and both
  run on the partitioned appraisal-sync endpoint (retry inside the partition), so the company row is written first.
- Known corner, accepted (same as #620): the newest Pending/NotSent row decides, so an earlier engagement's NotSent row
  on the same assignment silences the template of a later system assignment (e.g. admin assigned A without sending,
  A cancelled, quotation auto-assigns B). The admin can send from the slip (#623).
- Fee question for the template: external + quotation (Quotation) or manual (RoundRobin / Manual / Forced; the fee
  preview applies the Construction-Inspection carry-over itself).
- Declined in Matt review (do not re-raise):
  - `QueueAsync` 8 parameters (pre-existing), the shared try/catch shape in three consumers (local pattern), test
    helpers shared via `ExtoAssignmentEmailDispatchTests.NoEmailDispatcher()` / duplicated in the EXT test class.
  - No test drives `CompanySelectionActivity` with the phase-1 switch on: the agreed seams start at the two handlers;
    `CompanySelectionActivityTests` already cover the switch.
  - Decision "P" (admin-proceed-external) writes no email row, so the template is sent: same as `sendEmail` null
    (#627); the FE admin page never sends P with an email.
