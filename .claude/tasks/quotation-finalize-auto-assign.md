# Quotation finalize → auto-assign every appraisal

Mock: https://claude.ai/artifact/9uorDEb7GDkumm4sv24yAr

## Goal
On a **standalone** quotation (`/quotations/new`, `POST /quotations`: many appraisals, one winner),
pressing Finalize also assigns every appraisal that is waiting at `appraisal-assignment` to the
winning company, the same way the Administration page does today. A summary shows the outcome
per appraisal, and the finalizer is notified about appraisals that could not be assigned.

## Decisions (user, 2026-10-16)
- One winner for every appraisal in the quotation (`QuotationRequest.SelectedCompanyId`). Prices
  differ per appraisal.
- Appraisals that are not at `appraisal-assignment` when Finalize runs are **parked**. When one
  returns, the system only notifies; the int admin assigns it by hand on the Administration page.
  Nothing is auto-assigned later.
- The appraisal picker stays as it is (any Pending appraisal). Appraisals can leave mid-quotation,
  because `RequireNoActiveQuotation` is `ForwardOnly` and the Decision page can still route back (R)
  or cancel (C).
- Notifications go to the person who pressed Finalize, and only to them.
- While `ExternalCompanyAssignmentEnabled` is off, auto-assign is disabled and the reason is shown.

## Facts the design relies on
- Finalize does not touch any appraisal workflow today (`FinalizeQuotationCommandHandler`).
- Assign = `AssignAppraisalCommand` (`POST /appraisals/{id}/assignments`) → `ResumeWorkflowAsync`
  on `appraisal-assignment` with EXT + `assignmentMethod=quotation` + `selectedCompanyId`. Each resume
  runs in its own transaction (`WorkflowService.ResumeWorkflowAsync`).
- `CompanySelectionActivity` returns `no_match` (sending the case back to `appraisal-assignment`)
  when the go-live flag is off or the company is outside its MOU window. It fails (rollback) when
  the company is excluded for that appraisal.
- Follow-up staff: the PMA `sameAssigneeAsActivity` rule wins, then the admin's pick, then
  `InternalStaffRoundRobinService`.
- The fee comes from the winner's item via `CompanyAssignedIntegrationEventHandler.ResolveFeeSourceAsync`.
  It needs no change.

## Plan

### Backend (`dotnet-backend`, worktree `snapper`)
- [ ] 1. Migration on `appraisal.QuotationRequestAppraisals`: `AutoAssignResult` (nvarchar(20) null:
       Assigned | Failed | Parked | Skipped), `AutoAssignMessage` (nvarchar(500) null), `AutoAssignedAt`
       (datetime2 null). Add `FinalizedBy` to `QuotationRequests` only if no existing column or activity
       log already records the finalizer. One migration file. Do not apply it; the user runs `migrate`.
- [ ] 2. `GET /quotations/{id}/auto-assign-preview` (Dapper, IntAdmin only). Returns the global
       `externalAssignmentEnabled` flag, plus per appraisal: appraisalNumber, workflowInstanceId,
       current pending activity, assignment status, isPma, and `ready` (pending task at appraisal-assignment
       and assignment Pending).
- [ ] 3. Finalize request gets an optional `autoAssign: { followupMethod: "roundrobin"|"manual",
       staff: [{ appraisalId, staffId }] }`. Finalize commits first, exactly as today. Then, for each
       appraisal, in its own try/catch and transaction:
       - workflow cancelled → `Skipped`
       - not pending at appraisal-assignment → `Parked`
       - otherwise send `AssignAppraisalCommand` (EXT, quotation, SelectedCompanyId, assignedBy =
         **current user from the server**), then read where the instance landed:
         ext-appraisal-assignment → `Assigned`, back at appraisal-assignment → `Failed` (no_match),
         exception → `Failed` with the message
       - persist the result row and return the results in the response
       Guards: caller is IntAdmin; `autoAssign` while the go-live flag is off → 400; skip appraisals
       whose assignment is no longer Pending. First check whether a MediatR transaction behaviour
       wraps the finalize handler. If one does, run the loop outside it so a single failure cannot
       roll back the finalize or the other appraisals.
- [ ] 4. Quotation detail DTO: per appraisal add autoAssignResult/message/at, the live current activity,
       the live assignment status, and the follow-up staff (name and method). The summary reads live
       state, so an appraisal assigned by hand later shows as assigned.
- [ ] 5. Notifications (integration event → Notification consumer, modelled on `DocumentFollowupNotification`):
       after the loop, if any result is not `Assigned`: notify the finalizer, actionUrl `/quotations/{id}`.
       (User 2026-10-16: no notification when a parked appraisal returns. The admin monitors the summary.)
- [ ] 6. Tests covering each loop outcome (Assigned / Failed no_match / Failed exception / Parked / Skipped)
       and the "returned" notification trigger.

### Frontend (`react-expert`, new orca workspace on the FE repo, same branch name)
- [ ] 7. `FinalizeModal`: auto-assign switch (disabled with the reason when the flag is off), the preview
       table, follow-up mode (round robin / manual per row, plus "same person for all"), and staff from
       the existing eligible-assignees endpoint for `appraisal-book-verification` per instance. Send
       `autoAssign` with the finalize call.
- [ ] 8. `QuotationSelectionPage` Finalized state → summary table (result, current activity, follow-up,
       fee, reason, link to the appraisal's Administration task).
- [ ] 9. Two notification types (icons and th/en i18n) in `features/notification/types`.
- [ ] 10. `AdministrationPage`: use the Finalized quotation instead of `appraisalQuotations[0]`. Parked
        appraisals are assigned there by hand.

### QA
- [ ] 11. `test-automator` gates. BE: build (read the full output) and tests. FE: prettier, tsc `-b --force`,
        the full vitest suite (locale parity), lint, and vite build.
- [ ] 11b. `/code-review` (Skill, run against each worktree PATH) until clean. Fixes go back to the owning
        agent. After about 3 rounds, triage flip-flops and record the declined items below.
- [ ] 11c. `test-automator` runs the same gates again after the review is clean.
- [ ] 12. E2E on the user's running 3000/7111 with own fixtures only: a quotation with appraisals that
        are ready / parked (routed back via Decision) / excluded. Collect screenshots of the dialog,
        the summary and the notifications.

## Out of scope
- A winner per appraisal; auto-assign when a parked appraisal returns; a retry button.
- Pre-existing gaps (not fixed here): the Assign endpoint skips the workflow pipeline, and
  `POST /quotations` has no `EnsureAdmin`.

## Code-review triage log
Round 1 BE (10 findings). Accepted: post-commit errors no longer fail the finalize, the loop runs on
CancellationToken.None, one instance per appraisal (OUTER APPLY), a single save at the end, the re-read
after assign happens outside the try, the preview uses Dapper, the dead AppraisalId is removed, and
`QuotationAccessPolicy.IsAdmin` is added. Partly accepted: the workflow JSON `Variables` read is
removed, but PendingTasks/Instances reads stay (precedent: TaskCorrelationSql). **Declined:** moving the
loop to a background job. The user wants the summary immediately; a ponytail comment marks the ceiling.

Round 1 FE (10 findings). All accepted:
- 120 s timeout when autoAssign is sent
- refresh on settle
- whitelist for the live-state override
- a picker for every ready row
- the same-person list is the intersection of eligible staff
- AdministrationPage prefers the open quotation, then the latest Finalized one (by requestDate)
- gate on isFetching
- invalidate the task keys
- shared formatCurrency
- activity titles via getActivityConfig

Kept `staleTime: 0`: the app default is 5 min, so it is not redundant.

Round 2 BE (9 findings). Accepted:
- only domain exception messages are shown
- the endpoint fills `Failed` for every appraisal when the command crashes
- Failed instances count as ended
- not-Pending assignments are now recorded as `Skipped` (was `Parked`)
- the preview gets `workflowActive`
- the winner name comes from ICompanyLookupService
- no graph reload
- `AssignmentStatus.Pending.Code` replaces the literal
- the dead Username fallback is gone

**Declined:** slimming GetByIdWithAllDataAsync in the existing AssignAppraisalCommandHandler (pre-existing,
out of scope), and changing the IWorkflowRelayService contract to return the landing activity
(out of scope; the re-read works).

Round 2 FE (10 findings). All accepted:
- empty or short results → warning
- the modal cannot close while pending
- a timeout shows "may have completed"
- task invalidation in onSettled
- skipped preview rows
- staff query errors
- useCreateAssignment refreshes the quotation detail
- a shared invalidateTaskLists helper
- DESIGN.md fixes

The override rule stays Failed/Parked only, now correct because the BE records not-Pending as Skipped.

Round 3 BE (9 findings). Accepted:
- state re-read right before each assign
- a clearer generic engine-failure text
- Skipped no longer counts as incomplete
- soft-deleted appraisals filtered
- staffId validated against IntAppraisalStaff
- an appraisal with no winner price is Failed instead of being charged TotalQuotedPrice
- "not pending" wording

**Declined:**
- stale `assignedCompanyId` reuse: the pre-existing replay guard, also hit by manual Assign; would need a JSON read
- legacy Items fallback: standalone always writes link rows
- batching cleanup

Round 3 FE (7 findings). Accepted:
- 502/503/504 handled as "may have completed"
- skip labels match the BE rule
- Open Administration locks the pool task first
- removeQueries instead of invalidate, because of the async consumer
- dead methodLabel case removed
- per-id removal on error

**Declined:** sending autoAssign when 0 are ready. This is deliberate: it records Parked rows for monitoring.

Round 4 BE (4 findings). Accepted:
- the usable-price guard mirrors ResolveFeeSourceAsync
- a task locked by another admin is not resumed
- the AspNetUsers join uses NormalizedUserName

Conditional: CI carry-over is Skipped only if a typed column identifies it.

Round 4 FE (6 findings). Accepted:
- unpriced rows are not ready
- a preview error hides stale data
- a lock failure opens via OpenTask
- "same person for all" fills only loaded rows
- buttons are disabled while locking

Conditional: lockedTaskId is passed only on a fresh lock, if the API can tell.

Round 4 follow-up: Progressive (construction inspection) appraisals are excluded via `Appraisals.AppraisalType`.
This is conservative, because an internal-predecessor CI appraisal would not actually be forced.

Round 5 BE (4 findings). Accepted:
- a personal task belonging to another admin is not resumed
- CI is recorded as Failed (needs manual assign; counts as incomplete)
- the price guard requires every item to be priced
- endpoint fallback wording

**Declined:** persisting/notifying from the endpoint fallback.

Round 5 FE (4 findings). Accepted:
- the preview key is moved out of the detail prefix, and submit is blocked when the preview fails with the switch on
- Skipped is not counted in the toast
- the blank "same person" option clears

**Declined:** navigating away mid-request (the BE notification covers it).

Round 6 BE (1 finding, a money bug in pre-existing code that auto-assign made reachable):
`GetFinalizedByAppraisalIdAsync` ordered finalized quotations by RequestDate (draft creation). It now
orders by SelectedAt, and the list DTO gains `selectedAt` so AdministrationPage can order the same way.

Round 6 FE (5 findings). Accepted: the preview mirrors the BE via new `priced` / `taskHeldBy` /
`winnerAssignable` fields, and a stale staff pick counts as missing. **Declined:** the disabled query for a null
instance (ready rows always have one) and parallel eligible-assignee requests (performance).

**Loop stopped after round 6:** the findings had shrunk to preview-vs-backend alignment edge cases.
That was premature: the user asked to keep reviewing until clean (2026-10-17), so the loop resumed.

Round 7 BE (7 findings). Accepted:
- confirmed bug: `vw_QuotationList` has no SelectedAt, so GET /quotations returned 500. The column is added to the view.
- the outbox Publish failure no longer skips the save
- `ThenBy(Id)` tiebreaker on the finalized lookup
- the staff group check is cached per staffId
- one shared classifier for the loop and the preview; the preview returns predictedOutcome/reason

**Declined:**
- the stale assignedCompanyId check (repeat)
- endpoint fallback persistence (repeat)
- GetByIdWithAllDataAsync (repeat)
- cross-schema Dapper reads in the state reader (read side, same DB, no DbContext injected)

Round 7 FE (10 findings). Accepted:
- the preview ladder is replaced by the BE's predictedOutcome/reason. This fixes the parked-vs-winner ordering and deletes the old-API fallbacks.
- AssignedElsewhere requires the workflow to have left appraisal-assignment
- the preview is re-read after a 4xx
- XSS: companyName is no longer rendered via dangerouslySetInnerHTML
- Skipped rows have no Administration button

**Declined:**
- Dropdown reuse
- label helper de-duplication
- narrowing the detail-prefix invalidation
- removeQueries on error (perf only)

Round 8 BE (8 findings). Accepted:
- task at appraisal-assignment with a non-Pending row → Failed/assignmentNotReset (was a silent Skipped)
- unused preview fields trimmed (ready, workflowActive, isConstructionInspection, priced, assignmentStatus, winnerAssignable)
- PMA ignores a posted staffId

**Declined:**
- persist/retry of the final save
- price rule as a domain method
- shared constants
- cross-schema reads
- double state read

Round 9 BE (8 findings). Accepted:
- **ROOT-CAUSE money bug, pre-existing, also hits manual Assign.** CompanySelectionActivity's Manual/Quotation branch preferred the stale `assignedCompanyId`, which only the activity itself writes, over the admin's fresh `selectedCompanyId`. Now selectedCompanyId wins and assignedCompanyId is the fallback. This supersedes the earlier "stale assignedCompanyId declined" entries.
- endpoint fallback wording
- QuotationAccessPolicy.IsAdmin reuse
- dead AppraisalAssignState.Ready

**Declined:**
- perf/consumer (repeat)
- preview including soft-deleted rows
- shared key constant
- cross-schema reads

Round 8 FE (10 findings). Accepted:
- assignmentNotReset label; assignmentStatus dropped from the type
- stored Assigned but back at appraisal-assignment → 'Returned' + Administration button
- allowAutoAssign only on the standalone mount, not the task route
- award banner text when auto-assigning

**Declined:**
- null-instance staff picker (repeat)
- translating stored English messages (known limitation)
- preview hidden during refetch (deliberate)
- effective outcome on the BE
- AdministrationPage pick (repeat)

Round 10 BE (9 findings). Accepted:
- the go-live exemption now applies only when the company actually used equals the already-assigned one. This closes a regression from the round-9 precedence fix.
- the company name is resolved for the fresh pick and never borrowed from another company
- the state reader prefers the Pending assignment row

**Declined:**
- the fee resolver's TotalQuotedPrice fallback for manual assigns (pre-existing; raised with the user)
- persistence (repeat)
- perf (repeat)
- TOP 1 pending task
- cross-schema reads (repeat)
- constants (repeat)

Round 11 BE (7 findings). Accepted:
- validation of the autoAssign.staff body: null elements, duplicates
- QuotationNumber fallback in the notification

**Declined:**
- go-live exemption on the round-robin/forced paths (pre-existing "any assignedCompanyId" exemption; raised with the user)
- false Parked when the workflow is at ext-assignment with a Pending row (race)
- the fallback, perf and constant items (repeats)

Round 12 BE (10 findings). Accepted:
- money: the Quotation fee source is the newest finalized quotation WON BY the assigned company, not just the newest one that contains the appraisal
- the go-live exemption is defined around the company actually used, including the forced CI company. This fixes a regression from round 10.
- IsAdmin reuse inside QuotationAccessPolicy

**Declined:** all repeats (perf, persistence, TOP 1, deleted rows in preview, constants, cross-schema reads).

Round 13 BE (10 findings). Accepted:
- regression from round 12: a Quotation-method EXT assign to a non-winner is now rejected synchronously in AssignAppraisalCommandHandler (it used to throw in the async consumer)
- neutral generic failure text
- a null re-read after a committed assign → Assigned/unverified

Round 14 BE (9 findings). Accepted:
- the document follow-up gate, if configured on appraisal-assignment (relay skips the pipeline)
- a cheap AnyAsync for the winner guard
- the standalone-only rule enforced on the BE, plus a `standalone` flag on the preview

**Declined:**
- the excludedCompanyId pre-check (would need a Variables read)
- repeats

Round 14 BE result: appraisal-assignment DOES have the RequireDocumentFollowupCleared step (ForwardOnly). The classifier now fails these appraisals with reason documentFollowupOpen.

Round 15 BE (8 findings). Accepted:
- investigate SoD (excludeAssigneesFrom) and the appeal excluded company; add a classifier reason if the data is readable without Variables
- move the winner guard above the full load

**Declined:**
- superseded-winner billing (an admin decision)
- preview round trips
- repeats

Round 15 BE result:
- SoD: `excludeAssigneesFrom` is not configured on appraisal-assignment, so no change.
- Appeal `excludedCompanyId`: stored only in Variables, so no change; the neutral message stays.
- The winner guard was moved above the full load.

Round 16 BE (9 findings). Accepted:
- AssignAppraisal verifies that the workflow instance belongs to the appraisal. This is a pre-existing trust-boundary gap that the new guard made matter.
- the notification consumer hard-codes its type
- one shared predicate for the winner guard and the fee lookup

**Declined:**
- go-live exemption for a re-picked cancelled company and for round-robin re-selection (pre-existing; part of user question 3)
- the remaining repeats and cleanup items

Round 18 FE: reverted the R1 change in QuotationSection. It had hidden "New quotation" after a cancelled re-quote, so the section shows `quotations[0]` again. Lock failure now raises a toast.

Round 19 FE: lock branching keys on status. A 409 (held by someone else) shows a toast; any other refusal, such as not a pool task, navigates without a lock. Also accepted: an auto-assign success toast, the `hasRoleOrPermission` helper, and a comment fix.

Round 20 FE (8 findings): **nothing new accepted.** Every item was one of:
- a repeat decline (timeout refresh, preview hidden during refetch, null-instance picker, preview key placement, detail-key literal)
- pre-existing (plain-finalize timeout)
- intentionally reverted (QuotationSection pick)
- a rare race (another tab holds the lock and the open then fails)

→ **The FE review has converged (clean by the stop rule).** BE converged at round 17; final gates green after round 16 (no BE code changes since).

Round 17 BE (10 findings): **nothing new accepted.** Every item is one of these:
- a repeat decline: final-save persistence, endpoint fallback, perf, excludedCompanyId (Variables only), cross-schema reads, constants, the AutoAssignStaff field
- pending user question 3 (round-robin go-live)
- deliberate: the ownership check sits after the load so a missing appraisal still returns 404
- a rare race (workflow ended between resume and re-read)

→ **The BE review has converged (clean by the stop rule).**

Round 17 FE (7 findings). Accepted:
- result toasts move to the hook-level mutation callbacks, so they survive when the modal unmounts on a Finalized refetch mid-request
- the lock is derived from pickActiveQuotation

**Declined:** English messages and activity titles (known/pending), the null-instance picker (repeat), cleanup items.

Round 15 FE (10 findings). Accepted:
- no auto-assign from AdministrationPage. Its own open task would be completed under the form; the finalize task route keeps it.
- non-compact Dropdown in the toolbar

Round 16 FE (10 findings). Accepted:
- a shared active-quotation helper for AdministrationPage and QuotationSection
- 408/499 handled as "may have completed"
- controls disabled while the request is pending
- Open Administration shown to admins only
- revert edits to the unused QuotationStatusView

**Declined:** repeats and cleanup.

Round 14 FE (10 findings). Accepted:
- invalidate instead of remove for assignments/fees (removal on an active observer refetches anyway)
- preview Retry
- neutral incomplete toast, since the modal now opens on pages without the summary
- merge the `if (refused)` checks

**Declined:**
- dangerouslySetInnerHTML elsewhere (waiting on the user)
- pool vs group (same group)
- repeats

Round 13 FE (9 findings). Accepted:
- standalone gating from the quotation's data at every FinalizeModal mount, including the finalize task route. The route-param gate was wrong both ways.
- null-reason badge fallback
- NaN-safe AdministrationPage sort
- no task-list refetch on a 4xx

**Declined:**
- other dangerouslySetInnerHTML sites (waiting on the user)
- repeats

Round 12 FE (9 findings). Accepted:
- footer note when 0 ready but results get recorded
- Failed count in the footer
- Returned also overrides stored Skipped
- guard on `appraisals?.map`
- removeQueries narrowed to the assignments/fees keys and only when the finalize may have committed (supersedes the earlier decline)
- StaffSelect → shared Dropdown (DESIGN.md rule; raised 4×, now accepted)

**Declined:**
- invalidateTaskLists location
- router prop
- TONE dedupe

The award-body Trans escaping is proven by a mutation test: removing `escapeValue: true` turns the new `<strong>`-name test red.

Round 11 FE (10 findings). Accepted:
- Thai spacing after `<strong>`
- isAxiosError

**Declined:**
- other dangerouslySetInnerHTML sites with company names (pre-existing; raised with the user)
- the deliberate, repeat and cleanup items

Round 10 FE (10 findings). Accepted:
- the blank "same person" pick clears all rows
- table overflow-x-auto
- a single staff-query lookup
- bold winner name via `<Trans>`

**Declined:**
- the double toast
- other cache prefixes
- preview key placement
- the per-row requests and Dropdown (repeats)
- the null-reason label

Round 9 FE (10 findings). Accepted: Submit no longer waits on the preview when the switch is off.

**Declined:**
- navigation toasts
- timeout polling
- removeQueries
- null-instance picker
- fallback toast wording
- taskHeldBy as a user code
- per-row requests
- invalidateTaskLists location
- Dropdown

## Review
Status 2026-10-16: items 1–11c are done, uncommitted. Item 12 (E2E) is waiting on the user: 7111/3000
currently serve other worktrees, and the migration has not been applied.

**Final gates**
- BE: 0 build errors. Every unit project is green (Appraisal 742, Workflow 1128, Request 272, Integration 312, …).
  The migration is additive and the snapshot is clean.
- FE: tsc 0 errors. vitest 2379 passed (201/201 files). No new lint errors. vite build OK.

**What was built**
- BE: `FinalizeQuotationEndpoint` runs finalize (transactional, commits) and then, when `autoAssign` is sent,
  `AutoAssignQuotationAppraisalsCommand` (non-transactional, CancellationToken.None, one DI scope per
  appraisal).
- Classification per appraisal, re-read just before each assign:
  - Skipped: deleted / workflow ended / assignment not Pending
  - Parked: not at appraisal-assignment
  - Failed (no assign call): construction inspection / task held by another admin / winner unavailable /
    winner has no usable price / ineligible staff
  - otherwise the existing AssignAppraisalCommand runs, then Assigned / Failed (no_match / engine failure)
- Results go to 3 new columns on QuotationRequestAppraisals in one save. The finalizer gets one
  QuotationAutoAssignIncomplete notification when any result is Failed or Parked.
- `GET /quotations/{id}/auto-assign-preview` mirrors the same rules (ready / priced / taskHeldBy /
  winnerAssignable / workflowActive / isConstructionInspection).
- The quotation detail adds live per-appraisal state for the summary.
- Fee lookup fix: `GetFinalizedByAppraisalIdAsync` orders by SelectedAt. The list DTO gains `selectedAt`.
- FE:
  - FinalizeModal: switch, readiness table, round robin / manual follow-up with "same person for all"
    (intersection of eligible staff), timeout/5xx handling.
  - AssignmentResultSummary on the Finalized quotation page, with Open Administration (lock then open).
  - Notification type icon.
  - AdministrationPage picks the open quotation first, then the latest finalized.

**Known, accepted**
- Sync loop ceiling.
- All Progressive appraisals need a manual assign.
- Stale assignedCompanyId replay guard: pre-existing.
- Endpoint fallback is response-only.
- Activity titles are English-only.

## 2026-10-18 — Decision 1 (fee fallback) → reject, plus 5 review rounds

User decided: reject instead of the TotalQuotedPrice fallback ("the winner always quotes every appraisal; add the guard anyway").
- Consumer `ResolveFeeSourceAsync`: missing winner item → 400; winner pick = `IsWinner && CompanyId == company`; null lookup message covers "not won by this company / none".
- Sync guard `HasFinalizedWonByAsync` (assign path, so the admin sees the error before the workflow moves):
  - `LatestFinalized(appraisalId)` = Finalized + contains appraisal, ordered SelectedAt DESC, RequestDate DESC, Id; no company filter, so the latest round decides
  - then requires SelectedCompanyId == company AND the winner item is priced (same rule as the consumer)
  - SQL Server translation checked with ToQueryString
- `GetFinalizedByAppraisalIdAsync(appraisalId, Guid winnerCompanyId)`: required Guid, AsSplitQuery, returns the latest only if that company won it.
- `AssignAppraisalCommandHandler` guard:
  - runs after the ownership check, so it can't be used to probe winners
  - Quotation + decision EXT or P only; C and R are not blocked
  - separate 400 for a missing or invalid company id

Review rounds:
- R1: accepted P bypass, latest-RFQ mismatch, AsSplitQuery, move after ownership, tests.
- R2: accepted the allowlist (C/R were blocked — a regression from R1's fix), required Guid, inlined FinalizedFor, tests.
- R3: accepted "latest round first, then winner", consumer winner pick, missing-company message, test rename.
- R4: accepted the message and 2 mutation-proven tests.
- R5: no correctness findings; added legacy/zero-price consumer tests (mutation-proven) and a doc fix → **clean**.

Declined, with reasons given in chat:
- duplicate items per appraisal
- the fee rule duplicated in 4 places
- fee validation in CompanySelectionActivity
- forced CI company / CI fee swap
- extra EXISTS per auto-assign appraisal
- empty-company fallback
- filtered includes / null-path graph load
- load order
- cross-module ownership SQL / reader location
- P+Internal guarded
- carrying QuotationRequestId through the event for replays
- SQL Server integration test
- SelectedQuotationId
- redundant SelectedCompanyId check
- shared fixture
- an open newer RFQ vs the old finalized winner (relay bypasses RequireNoActiveQuotation for every method; pre-existing, FE-enforced)

Gates: solution build 0 errors; no pending model changes; Appraisal 797, Workflow 1139 (+1 skipped), Notification 32.

## 2026-10-18 — E2E (user-authorized migrate + stopping tilefish on 7111/3000)
A/B/C/D all PASS:
- fees = winner's per-appraisal ex-VAT price
- guard 400s correct; R is not blocked
- Admin-page EXT/INT assign OK
- notification sent once; no new error-queue messages

4 UI issues found and fixed in FE: award dialog width, follow-up "—" right after finalize, Open Administration landing, double period after "Ltd.".

FE review loop on those fixes:
- R1: accepted opt-in stateless polling, finalizedWith copy, badge+reason line (DESIGN.md), Firefox td max-w, modal size, test cap
- R2: accepted poll anchored on newest stamp, size flash while loading, taskHeldUnknown, parked null activity + unknown default, parkedNote copy
- R3: accepted single availability rule / if-chain, summary td max-w, no PMA reason line
- R4: accepted Returned note, tone fallback, blank toast, OUTCOME_KEYS map, isReasonCode guard
- R5: trivial only (409 toast trim, `=== true`, Thai note) → **clean**

Gates caught a real tsc error from a template-literal i18n key (fixed with a typed REASON_KEYS map) and a no-explicit-any in finalizeToasts.

Final FE gates: tsc 0; vitest 205 files / 2430 passed; no new lint; prettier OK; build OK. Screenshots in scratchpad shots-fe6/.

Declined:
- size jump when the preview's availability differs from the cache
- taskHeldBy shows a user code
- TaskIndexRedirect default landing
- landOn per-task-type check
- shared label/hasKey helpers
- PMA rows in the poll (no FE field)
- poll on the full detail endpoint
- already-held lock release / lost-response lock
- Skipped/noAssignment shown as Returned (needs reason code = pending decision 2)
- merging outcome maps

Fixtures left in dev DB: see scratchpad fixtures.md (incl. WinnerTentative QTN-000109-2569).

## 2026-10-18 — User: "fix all 3 pending decisions, then review, then E2E"
1. **Reason code**
   - Added an `AutoAssignReasonCode` column, merged into ONE migration, `20261018112847_AddQuotationAutoAssignResult` (4 columns).
     - Rolled back the dev DB first by dropping the 3 columns and the history row; only fixture QA had data.
   - `AutoAssignReason` constants (21 codes); `autoAssignReasonCode` added to the detail DTO and `reasonCode` to the finalize results.
   - A committed assign that lands at an unexpected activity is now recorded as Assigned + `workflowElsewhere`, the same as `unverified`.
   - FE translates reasons by code (en/th/zh); `assignRejected` = prefix + stored detail.
   - Skipped→Returned only for `notPending`; the Open Administration button depends on live state only.
2. **Go-live**
   - CompanySelectionActivity resolves the company once. Round robin is exempt only when the replay guard reuses the company; a fresh pick while the switch is off → no_match.
   - (b) a re-picked cancelled company: no workflow signal exists (assignedCompanyId survives cancel/route-back) → **the user accepted the gap**; noted in a code comment.
   - The forced path fills an empty name from the company.
3. **XSS**: the new `BoldTrans` (Trans with escapeValue:true + shouldUnescape) replaces all 8 dangerouslySetInnerHTML sites, and FinalizeModal also uses it.
   - `<strong>` moved into the locale strings; sibling keys `cancel.bodyRich` / `selectWinner.bodyRich` for the plain ConfirmDialog callers.
   - Hostile-name tests added.

Reviews:
- BE: R1 (resolve once), R2 (cleanup only) → clean.
- FE: R1–R4 accepted (the live-state rules for workflowElsewhere oscillated until the root cause was moved to the BE), R5 trivial only → clean.

Declined:
- {{ re-interpolation in names
- the quotationAwarded period
- twin keys
- mixed th/en `assignRejected` detail
- in-flight stale engagements
- Manual→round robin while the switch is off
- persisting `unexpectedStop`

Gates:
- BE: full solution build 0 errors; all 12 unit projects green (Appraisal 800, Workflow 1142).
- FE: tsc 0; vitest 2459; build OK; no new lint.

Migrated again (user authorized E2E); E2E round 2 is running.

## 2026-10-18 — E2E round 2: ALL PASS
Scenarios:
- **A.** QTN-000110-2569, 7 appraisals.
  - Outcomes: Assigned×3, Parked/notAtAssignment, Failed/documentFollowupOpen, Skipped/notPending, Skipped/workflowEnded.
  - Codes are in the DB and in the API; the summary reads correctly in Thai.
  - Fees are the per-appraisal ex-VAT price; 1 notification sent; queues unchanged.
- **B.** Open Administration → the /administration tab, on the needed row only.
- **C.** A hostile company name was walked live through every reachable site: no img, no onerror, no dialog; one `<strong>`.
- **D.** Admin EXT/INT assign, the winner/price guard (400 vs winner OK, item fee), and GET /quotations all OK.
- **E.** Go-live switch, with user approval: off for ~20 s via PUT /system-configurations.
  - Award dialog disabled with a reason; finalize+autoAssign → 400.
  - Round robin and manual EXT → no_match and no company.
  - Restored to true and verified.

Fixtures left in the dev DB:
- appraisals 69105505–69105520 (505/506 are NOT ours)
- QTN-000107..111 + QE (WinnerTentative)
- fixture company 01a14ef6-… with a hostile name and 4 users, all INACTIVE; the user chose to keep them

Pre-existing bugs seen (not this branch):
- a company username >10 chars breaks workflow PendingTasks.UpdatedBy (truncation)
- appraisal-cancelled-webhook: "Invalid column name 'CreatedOn'"
