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
- [x] /code-review r1 (8 findings): fixed an email that cannot be readied at the handover → Failed (EXT and EXTO),
      fee question computed once, log text, ordering comment
- [x] /code-review r2 (7 findings): a template that cannot be built → empty Failed row (EXT and EXTO); EXTO template
      added before it is queued; `MarkFailed` back to Queued-only, new `FailToPrepare` for the dispatcher; tests
- [ ] /code-review r3+ (fix diff only), gates (test-automator)
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
- An email the handover could not ready (Auth/DB lookup throws) is marked **Failed** with the fixed reason
  "ระบบเตรียมอีเมลไม่สำเร็จ" in the same save, for EXT and EXTO, so it never stays Pending (user, from /code-review r1).
  Domain `FailToPrepare` (dispatcher only); `MarkFailed` stays Queued-only for Notification's reports. No bell
  notification for this case (the slip shows Failed; resend works).
- A template that cannot be built at all is added as an **empty Failed row** (no subject/body, same reason), EXT at
  CompanyAssigned and EXTO at the handover (user, from /code-review r2). app#538's history must render an empty subject.
- Declined in /code-review r1 (do not re-raise):
  - A dead-lettered CompanyAssigned redriven after InternalFollowupAssigned leaves its template row Pending: needs a
    dead-letter plus an out-of-order redrive; accepted.
  - A stale Pending row of an earlier engagement sent in place of the template: its main source (a swallowed queue
    failure) is now Failed; the failed-cleanup source is the #620 accepted corner.
  - Admin's manual follow-up baked into the body while a PMA completer wins the Cc: the admin's text is their intent (#620).
  - AssignedBy of a route-back to a new company is the earlier admin: the row still records them; only the failure
    notice recipient differs.
  - no_match keeps the row's earlier officer: on a route-back replay that officer is this engagement's real follow-up,
    clearing would be wrong.
  - no_match still loads/saves the aggregate (UpdatedAt bump): harmless; only the log text was fixed.
- Declined in /code-review r2 (do not re-raise):
  - A transient error (deadlock, timeout) becomes a permanent Failed instead of a retry: the trade-off the user chose in
    r1 (Failed + resend over retry / dead-letter, which would hold up the follow-up).
