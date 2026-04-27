# Quotation-Driven Appraisal Assignment (IBG) — Feature Summary

**Purpose**: a study + test guide for the newly implemented feature. Use this to verify the end-to-end flow, understand the architecture, and know what to do before spinning up the system.

**Status**: implementation complete across backend + frontend; build is green; pending the Track 4.3 re-review pass.

---

## 1. What this feature does

For **IBG banking segment** appraisal requests, fees are not derived from selling price. Instead, the bank runs a **competitive RFQ (Request-for-Quotation)** process:

1. Bank Admin, while reviewing the `appraisal-assignment` task, clicks **"Request Quotation"** (a side action — the task stays open).
2. External appraisal companies receive invitations and submit bids (price + ETA + terms).
3. At the due date the quotation auto-closes; admin is notified.
4. **Admin reviews all bids** and toggles a **shortlist** (can include all or narrow down).
5. Admin **sends the shortlist to RM** (`= Request.Requestor`).
6. RM picks a **tentative winner**. Admin can override at any time.
7. Admin **negotiates** with the tentative winner (max 3 rounds per company). Company can Accept / Counter / Reject.
8. If the tentative winner rejects, the request returns to step 4 — admin can reshortlist and resend.
9. When terms are agreed, **Admin clicks Finalize** — records the final negotiated price on the RFQ. The admin task **remains open** (no auto-completion).
10. Admin then **manually clicks "Route External"** on the still-open task. The frontend auto-fills the winning company + `assignmentMethod="Quotation"` into the task-completion payload.
11. The workflow engine advances to `CompanySelectionActivity`, which honors the `"Quotation"` method and publishes `CompanyAssignedIntegrationEvent`.
12. `CompanyAssignedIntegrationEventHandler` links the `QuotationRequest` to the `AppraisalAssignment` and creates the `AppraisalFee` at the finalized price **in a single unit of work**.
13. Workflow continues to `ext-appraisal-assignment` as normal.

Cancelling the quotation at any non-Finalized status returns control to the admin task for a different decision.

---

## 2. State machine (QuotationRequest.Status)

```
  Draft ──Send()──▶ Sent ──Close()──▶ UnderAdminReview ──SendShortlistToRm()──▶ PendingRmSelection
                                          ▲                                            │
                                          │                                            │ PickTentativeWinner()
                                          └─────────RecallShortlist()◀─────────────────┤
                                                                                       ▼
                                                                                WinnerTentative
                                                                                ▲    │
                                                                  EndNegotiation│    │ StartNegotiation()
                                                                                │    ▼
                                                                                │ Negotiating
                                                                                │    │
                                                         RejectTentativeWinner()│    │ Finalize()
                                                         (→ UnderAdminReview)   │    │
                                                                                │    ▼
                                                                               Finalized
                                                                                │
  (any status except Finalized) ─────────── Cancel() ──────────▶ Cancelled
```

CompanyQuotation sub-states: `Submitted | UnderReview | Tentative | Negotiating | Accepted | Rejected | Withdrawn`, plus an `IsShortlisted` flag. `NegotiationRounds` capped at `MaxNegotiationRounds = 3` per company; the budget resets when the tentative winner is replaced.

---

## 3. Architecture (wiring)

### Backend

- **Aggregate**: `QuotationRequest` (`Modules/Appraisal/Appraisal/Domain/Quotations/QuotationRequest.cs`) owns the full lifecycle. Linked to an Appraisal + Request + WorkflowInstance + TaskExecution + BankingSegment at creation.
- **Side-process, not a new workflow activity**: admin task at `appraisal-assignment` stays open throughout. Quotation events do NOT auto-complete the task.
- **Integration events** published via `IIntegrationEventOutbox` (DB outbox pattern — buffered with the same transaction as domain state changes):
  - `QuotationStartedIntegrationEvent` — triggers invitations.
  - `QuotationSubmissionsClosedIntegrationEvent` — notifies admin pool.
  - `ShortlistSentToRmIntegrationEvent` — notifies RM.
  - `TentativeWinnerPickedIntegrationEvent` — notifies admin + winning company.
  - `QuotationFinalizedIntegrationEvent` — notifies winner + RM (no assignment yet).
  - `QuotationCancelledIntegrationEvent` — notifies invited + RM.
- **Assignment + Fee creation**: lives in `CompanyAssignedIntegrationEventHandler` (Appraisal module). When `AssignmentMethod == "Quotation"`, it calls `IQuotationRepository.GetFinalizedByAppraisalIdAsync`, links `QuotationRequestId` onto the `AppraisalAssignment`, and creates `AppraisalFee` atomically with the existing unit of work.
- **Auto-close**: `QuotationAutoCloseService : BackgroundService` polls every 60s for `Status='Sent' AND DueDate<=now` and sends `CloseQuotationCommand` (idempotent).
- **Authorization**: `QuotationAccessPolicy` (`Modules/Appraisal/.../Quotations/Shared/`) enforces scoping inside command handlers (not just endpoints):
  - Admin: unrestricted.
  - RM: only for requests where `Request.Requestor == me` and `status ≥ PendingRmSelection`; GET responses strip non-shortlisted bids.
  - ExtAdmin: only for invitations where `CompanyId` matches JWT `company_id` claim; GET responses strip other companies' bids.
- **`RmUserId` is resolved server-side** from `request.Requests.Requestor` at quotation-start time (never taken from the client).
- **Workflow integration**:
  - `appraisal-workflow.json` is **unchanged** — no new activity type.
  - `CompanySelectionActivity` now has a `"Quotation"` branch (file: `Modules/Workflow/Workflow/Workflow/Activities/CompanySelectionActivity.cs`) that takes `assignedCompanyId` from workflow variables (set by the admin's task completion) and publishes `CompanyAssignedIntegrationEvent` with `AssignmentMethod="Quotation"`.

### Frontend

- **Admin task page** (`src/features/appraisal/pages/AdministrationPage.tsx` + `src/features/appraisal/components/QuotationSection.tsx`): status-aware panels render different UI per quotation state (Sent countdown → AdminShortlistPanel → ShortlistSentPanel → NegotiationPanel → Finalized summary).
- **RM page** at `/quotations/:id` (`src/features/quotation/pages/QuotationSelectionPage.tsx`): shortlisted bids only, "Pick as tentative winner" action.
- **ExtCompany portal**:
  - `/ext/quotations` — invitation list scoped to the JWT `company_id`.
  - `/ext/quotations/:id` — submit form + respond-to-negotiation panel.
- **Contract**: `QuotationStatus` Zod enum is PascalCase to match backend; `GET /quotations` list accepts `?appraisalId=…` for the Route-External lookup; `PUT /quotations/{id}/submit` (ext-company submit) derives companyId from JWT.

---

## 4. New API surface

All under `/quotations` (add `.RequireAuthorization()` everywhere; role enforcement via `QuotationAccessPolicy`).

| Method | URL | Role | Purpose |
|---|---|---|---|
| POST | `/quotations/start-from-task` | Admin | Start RFQ (side-effect; task stays open). Body: `{appraisalId, requestId, workflowInstanceId, taskExecutionId, dueDate, invitedCompanyIds[], specialRequirements?, appraisalNumber, propertyType, propertyLocation?, estimatedValue?, bankingSegment?}`. |
| POST | `/quotations/{id}/invite` | Admin | Add more invitations (Draft only). |
| PUT | `/quotations/{id}/submit` | ExtAdmin | Submit bid; `companyId` from JWT. |
| POST | `/quotations/{id}/close` | Admin or SYSTEM | Idempotent. Sent → UnderAdminReview. |
| POST | `/quotations/{id}/quotations/{companyQuotationId}/shortlist` | Admin | Mark shortlisted. |
| DELETE | `/quotations/{id}/quotations/{companyQuotationId}/shortlist` | Admin | Unmark. |
| POST | `/quotations/{id}/send-to-rm` | Admin | UnderAdminReview → PendingRmSelection. |
| POST | `/quotations/{id}/recall-shortlist` | Admin | PendingRmSelection → UnderAdminReview. |
| POST | `/quotations/{id}/pick-tentative-winner` | RM or Admin | Body: `{companyQuotationId, reason}`. |
| POST | `/quotations/{id}/negotiations/open` | Admin | Body: `{companyQuotationId, proposedPrice, message?}`. |
| POST | `/quotations/{id}/negotiations/{negotiationId}/respond` | ExtAdmin | Body: `{verb: Accept\|Counter\|Reject, counterPrice?, message?}`. |
| POST | `/quotations/{id}/reject-tentative-winner` | Admin | Body: `{reason}`. Returns to UnderAdminReview. |
| POST | `/quotations/{id}/finalize` | Admin | Body: `{companyQuotationId, finalPrice, reason?}`. |
| POST | `/quotations/{id}/cancel` | Admin | Body: `{reason}`. |
| GET | `/quotations/{id}` | Admin/RM/ExtAdmin (auto-scoped) | Returns `companyQuotations[]` + all IBG fields. |
| GET | `/quotations?appraisalId={id}` | Admin/RM/ExtAdmin (auto-scoped) | Role-scoped list. |

---

## 5. Setup before testing

### 5.1 Apply the DB migration

```
dotnet ef database update --project Modules/Appraisal/Appraisal --startup-project Bootstrapper/Api
```

This adds columns: `AppraisalId`, `RequestId`, `WorkflowInstanceId`, `TaskExecutionId`, `BankingSegment`, `RmUserId`, `SubmissionsClosedAt`, `ShortlistSentToRmAt`, `ShortlistSentByAdminId`, `TotalShortlisted`, `TentativeWinnerQuotationId`, `TentativelySelectedAt`, `TentativelySelectedBy`, `TentativelySelectedByRole`, `RowVersion` on `QuotationRequests`; `IsShortlisted`, `OriginalQuotedPrice`, `CurrentNegotiatedPrice`, `NegotiationRounds` on `CompanyQuotations`; nullable `QuotationItemId` on `QuotationNegotiations`; 3 indexes; CK constraints on both Status columns.

### 5.2 Redeploy the `vw_QuotationList` view

```
Database/Scripts/Views/Appraisal/vw_QuotationList.sql
```

now includes `AppraisalId` and `RmUserId`. The `.csproj` wildcards should pick it up on next build/deploy; for a running dev DB, execute the `CREATE OR ALTER VIEW` manually against `localhost,1433`.

### 5.3 Add ext-company menu entry (optional, see §7)

See §7 for two options (admin UI vs seed code).

### 5.4 Verify JWT `company_id` claim

The Auth module already emits `company_id` in tokens for users with a `CompanyId`. If you have ExtAdmin users whose `CompanyId` field isn't populated in the DB, set it via `/admin/users` before testing ext-company submission.

### 5.5 Boot the stack

```
docker compose up -d        # SQL Server, Redis, RabbitMQ, Seq
dotnet run --project Bootstrapper/Api
# in frontend repo:
pnpm install && pnpm dev
```

---

## 6. End-to-end test plan

Use three browsers / incognito windows to simulate Admin / RM / ExtCompany concurrently.

### 6.1 Pre-reqs

- Admin user (roles include `Admin`).
- RM user (role `RM`, will be `Requestor` on a test request).
- 3 ExtAdmin users from 3 different companies (role `ExtAdmin`, each with a distinct `CompanyId` claim). Each company's `LoanTypes` should include `IBG` for auto-suggestion to work (optional).

### 6.2 Create an IBG request

As Admin or the RM user, `POST /requests` with `loanDetail.bankingSegment = "IBG"`. Proceed through initial approvals until the workflow parks at `appraisal-assignment`.

### 6.3 Start the quotation

1. Log in as Admin, open the admin task at `/tasks?activityId=appraisal-assignment`, open the specific appraisal.
2. In the Quotation panel, click **Create a new quotation**. Pick 3 companies (auto-suggest preselects by loan type), set due date ~2h from now, send.
3. **Verify**:
   - Admin task still shows as "In Progress" (not completed).
   - `GET /quotations/{id}` returns `status = "Sent"`, `invitations[]` populated, `rmUserId` populated from the Request's Requestor.
   - Each invited ExtAdmin gets a SignalR + DB notification.

### 6.4 Companies submit bids

For **at least 2 of the 3** ext-company users:
1. Log in, visit `/ext/quotations`.
2. Open the invitation, submit a bid (price, ETA, validity, remarks).
3. **Verify** backend: `GET /quotations/{id}/quotations[company]` appears with `status="Submitted"`.

Verify that a 4th (non-invited) ExtAdmin attempting `PUT /quotations/{id}/submit` gets **403**.

### 6.5 Auto-close

Either wait for the due date (auto-close runs every 60s) OR fire `POST /quotations/{id}/close` as Admin.

**Verify**: status transitions to `UnderAdminReview`. Admin receives the "review shortlist" notification.

### 6.6 Admin review + shortlist

1. Admin refreshes the task page. The QuotationSection now shows **AdminShortlistPanel** with the received bids.
2. Toggle shortlist on 2 of the 2 submitters (or 1 of 2 to test narrowing).
3. Click **Send to RM**.

**Verify**: status is `PendingRmSelection`. RM receives a "pick a winner" notification. `ShortlistSentToRm` event was produced.

### 6.7 RM picks tentative winner

1. Log in as RM, visit `/quotations/:id` (URL from the notification).
2. **Verify**: only the shortlisted bids are visible. An unshortlisted bid fetched directly via `GET /quotations/{id}` as RM also returns shortlist-only.
3. Pick one company, enter reason, submit.

**Verify**: status is `WinnerTentative`. Admin and the winning company get notified.

### 6.8 Admin negotiates

1. Back as Admin, the QuotationSection now shows **NegotiationPanel**. Click "Open negotiation round" with a proposed price lower than the bid + a message.
2. Log in as the winning ExtAdmin, open the quotation, see the open negotiation. Respond with **Accept**.
3. **Verify**: `CompanyQuotation.NegotiationRounds = 1`, `CurrentNegotiatedPrice = proposedPrice`, parent status back to `WinnerTentative`.
4. Try opening a 4th round — expect the backend to reject with an invariant error (`MaxNegotiationRounds` = 3).

### 6.9 Admin finalizes

1. Admin clicks **Finalize** in the NegotiationPanel. Confirms price (can match current negotiated price).
2. **Verify**: 
   - `QuotationRequest.Status = "Finalized"`.
   - Winning CompanyQuotation has `IsWinner=true`, `Status="Accepted"`.
   - **Admin task is STILL open** (this is the product requirement).
   - Winning company + RM receive notification.
   - `QuotationFinalizedIntegrationEvent` lands in the outbox.

### 6.10 Admin clicks Route External

1. Admin, still on the task page, now sees the Finalized summary panel. The assignment form for Route External should pre-select `method = Quotation` with the winning company locked in.
2. Submit the task decision (Route External).

**Verify**:
- Task completes with decision=EXT, `assignmentMethod=Quotation`, `assignedCompanyId=winner`.
- Workflow advances to `CompanySelectionActivity` → publishes `CompanyAssignedIntegrationEvent` with `AssignmentMethod="Quotation"`.
- `CompanyAssignedIntegrationEventHandler` creates:
  - `AppraisalAssignment` (External, Quotation, AssigneeCompanyId=winner, Status=Assigned, `QuotationRequestId` set).
  - `AppraisalFee` with a single item at the final negotiated price.
- Workflow proceeds to `ext-appraisal-assignment` with the winning company's team ready to act.

**Check the DB:**
```sql
SELECT * FROM appraisal.AppraisalAssignments WHERE AppraisalId = '<id>' ORDER BY AssignedAt DESC;
SELECT f.*, i.FeeAmount FROM appraisal.AppraisalFees f
  JOIN appraisal.AppraisalFeeItems i ON i.AppraisalFeeId = f.Id
  WHERE f.AssignmentId = '<new assignment id>';
SELECT Status, IsWinner, CurrentNegotiatedPrice FROM appraisal.CompanyQuotations WHERE QuotationRequestId = '<rfq id>';
```

### 6.11 Cancel path (separate run)

- Start a new quotation, then immediately `POST /quotations/{id}/cancel` with a reason.
- **Verify**: status `Cancelled`; admin task still open (admin now picks any other decision); invited companies + RM notified.

### 6.12 Reject-tentative path (separate run)

- After RM picks a tentative winner, the admin opens a negotiation round; winning company **Rejects**.
- **Verify**: winning CompanyQuotation `Withdrawn`; RFQ returns to `UnderAdminReview`.
- Admin re-shortlists (or keeps existing) and re-sends to RM. RM picks a different company. Negotiation round budget resets for the new tentative winner.

---

## 7. Ext-company menu entry (MANUAL step)

The RoleProtectedRoute for ExtAdmin at `/ext/quotations` is wired in `src/app/router.tsx`, but menu items are DB-seeded and permission-gated, so the link won't appear in the sidebar by default.

**Option A — Live via admin UI:**
1. Go to `/admin/permissions`, create permission `QUOTATION_SUBMIT_VIEW`.
2. Go to `/admin/roles`, grant `QUOTATION_SUBMIT_VIEW` to the `ExtAdmin` role.
3. Go to `/admin/menus`, add a root item:
   - Item Key: `main.ext-quotations`
   - Label: `My Quotations`
   - Icon: `file-invoice-dollar` / Solid / `text-emerald-500`
   - Path: `/ext/quotations`
   - View Permission: `QUOTATION_SUBMIT_VIEW`
4. Log the ExtAdmin user out and back in.

**Option B — Code seed (survives reseed in dev):**

Add one entry in `Modules/Auth/Auth/Infrastructure/Seed/MenuSeedData.cs:GetMainMenuSeed()`:

```csharp
new("main.ext-quotations", "My Quotations", "file-invoice-dollar", IconStyle.Solid, "text-emerald-500",
    "/ext/quotations", "QUOTATION_SUBMIT_VIEW", null),
```

Also add `QUOTATION_SUBMIT_VIEW` to the permission seed and grant it to the `ExtAdmin` role seed in `AuthDataSeed.cs`. Seeder is **INSERT-ONLY** — it won't overwrite existing menu edits, so use Option A if the API has already booted against the current DB.

---

## 8. Known gaps / limitations / things to watch

### Pre-existing (not this feature)

- `Tests/Integration` project has unrelated build errors (apphost copy failure + stale test field warnings). Not blocking runtime.
- Several `Option is deprecated` TypeScript warnings in `AdministrationPage.tsx` — from a `@deprecated`-tagged `Option` component used by other sections; predates this feature.

### Handed-off as noted

- Workflow consumer auto-completion of admin tasks was **deleted** per product change; admin always clicks Route External manually. If this is reverted later, reintroduce the `QuotationFinalizedIntegrationEventConsumer` in `Modules/Workflow` and add a typed (non-string-prefix) SYSTEM caller mechanism to `ValidateTaskOwnershipStep`.
- `QuotationAutoCloseService` has no distributed lock. Safe today because `Close()` is idempotent, but two API pods will duplicate the scan.
- `OrderByDescending(AssignedAt)` to find the active assignment (`CompanyAssignedIntegrationEventHandler.cs:41-44`) is brittle under routeback — relies on project convention of reusing rows.
- `CK_QuotationRequests_Status` retains `'Closed'` intentionally for the legacy `SelectQuotation` code path; new IBG flow never hits that status.

### To verify at first production deploy

- DB migration applied + `vw_QuotationList` view redeployed.
- Ext-company menu entry present (Option A or B).
- MassTransit EF outbox is configured globally (the feature assumes it; grep for `AddEntityFrameworkOutbox` / `UseBusOutbox` in `Program.cs`). If NOT configured, all `IIntegrationEventOutbox.Publish` still works (it writes to the custom `IntegrationEventOutbox` table in the Appraisal module — a separate mechanism from MassTransit's built-in outbox).

---

## 9. File inventory (what to code-review)

### Backend

**Domain (modified)**
- `Modules/Appraisal/Appraisal/Domain/Quotations/QuotationRequest.cs` — extended state machine.
- `Modules/Appraisal/Appraisal/Domain/Quotations/CompanyQuotation.cs` — shortlist + negotiation fields.
- `Modules/Appraisal/Appraisal/Domain/Quotations/QuotationNegotiation.cs` — nullable `QuotationItemId`.
- `Modules/Appraisal/Appraisal/Domain/Quotations/IQuotationRepository.cs` — added `GetByIdWithNegotiationsAsync`, `GetFinalizedByAppraisalIdAsync`.
- `Modules/Appraisal/Appraisal/Domain/Appraisals/AppraisalAssignment.cs` — added `SetQuotationRequestId`.

**Application (new folders)**
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/{StartQuotationFromTask,CloseQuotation,ShortlistQuotation,UnshortlistQuotation,SendShortlistToRm,RecallShortlist,PickTentativeWinner,OpenNegotiation,RespondNegotiation,RejectTentativeWinner,FinalizeQuotation,CancelQuotation,SubmitQuotation}/` — command, handler, endpoint each.
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/Shared/QuotationAccessPolicy.cs`.

**Application (modified)**
- `Modules/Appraisal/Appraisal/Application/EventHandlers/CompanyAssignedIntegrationEventHandler.cs` — Quotation branch + fee creation.
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/GetQuotationById/*` — now returns full DTO + auth-scoped.
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/GetQuotations/*` — auth + role-scoped + `?appraisalId=` filter.

**Infrastructure**
- `Modules/Appraisal/Appraisal/Infrastructure/Configurations/QuotationConfiguration.cs` — new columns, `RowVersion`, indexes.
- `Modules/Appraisal/Appraisal/Infrastructure/Repositories/QuotationRepository.cs` — `GetFinalizedByAppraisalIdAsync`.
- `Modules/Appraisal/Appraisal/Infrastructure/BackgroundServices/QuotationAutoCloseService.cs` — polls due dates.
- `Modules/Appraisal/Appraisal/Infrastructure/Migrations/20260421124425_AddQuotationIbgShortlistNegotiation.cs`.

**Workflow**
- `Modules/Workflow/Workflow/Workflow/Activities/CompanySelectionActivity.cs` — `"Quotation"` method branch.
- `Modules/Workflow/Workflow/Workflow/Pipeline/Steps/ValidateTaskOwnershipStep.cs` — reverted to pre-feature state (SYSTEM bypass removed).

**Shared / Events**
- `Shared/Shared.Messaging/Events/Quotation*IntegrationEvent.cs` — 6 events.

**Notification**
- `Modules/Notification/Notification/Application/EventHandlers/Quotation*NotificationHandler.cs` — 6 handlers.

**Database**
- `Database/Scripts/Views/Appraisal/vw_QuotationList.sql` — new `AppraisalId` + `RmUserId` columns.

### Frontend

**Modified**
- `src/app/router.tsx` — added `/quotations/:id`, `/ext/quotations`, `/ext/quotations/:id`.
- `src/features/appraisal/api/administration.ts` — `useCreateQuotation` points at new endpoint; `useAddToQuotation` removed.
- `src/features/appraisal/components/QuotationSection.tsx` — status-aware panel renderer.
- `src/features/appraisal/components/CreateQuotationModal.tsx` — new payload shape, no `rmUserId`.
- `src/features/appraisal/components/AddToQuotationModal.tsx` — empty tombstone.
- `src/features/appraisal/pages/AdministrationPage.tsx` — Route-External hook for quotation winner.
- `src/features/appraisal/types/administration.ts` — PascalCase status union; IBG fields.
- `src/features/auth/types.ts` — `companyId?: string` on `User`.
- `src/shared/config/navigationTypes.ts` — `'RM'` added to `UserRole`.
- `src/features/userManagement/roleMeta.ts` — `RM` entry.

**New (all under `src/features/quotation/`)**
- `api/quotation.ts` — 16 hooks.
- `schemas/quotation.ts` — Zod schemas + form schemas.
- `pages/QuotationSelectionPage.tsx` (RM).
- `pages/ExtCompanyInvitationListPage.tsx`.
- `pages/ExtCompanySubmitQuotationPage.tsx`.
- `components/{QuotationStatusBadge,AdminShortlistPanel,SendToRmModal,ShortlistSentPanel,NegotiationPanel,NegotiationModal,RejectTentativeModal,FinalizeModal,RespondNegotiationPanel}.tsx`.

---

## 10. Planning artefacts

- Plan file: `/Users/gky/.claude/plans/based-on-the-existing-soft-pnueli.md`
- Track 1 handoff: `.claude/tasks/quotation-driven-assignment-track1.md`
- This summary: `.claude/tasks/quotation-feature-summary.md`

---

## 11. Review history

1. **Track 4 review**: found 5 blockers (incomplete GET, unauthed list, client-supplied RmUserId, SYSTEM bypass, two-save finalize race). Product-level decision also landed at this stage: no system auto-completion of the admin task.
2. **Track 4.1 fix-up**: all 5 blockers closed. Auto-completion removed, SYSTEM bypass removed, assignment+fee moved into `CompanyAssignedIntegrationEventHandler` (single unit of work).
3. **Track 4.2 re-review**: all 5 original blockers verified closed, but 2 new wiring gaps identified: missing `?appraisalId` filter on `GET /quotations`, and no `"Quotation"` branch in `CompanySelectionActivity`. Fixed directly.
4. **Track 4.3 followups** (this pass): 5 non-blockers cleaned up — repo call for QuotationRequest read, outbox pattern everywhere, exception logging, fee zero-fallback, migration comment.
5. **Track 4.3 re-review**: **PASS / ship**. All 5 followups + 2 direct fixes verified closed. No blocker or warning-class issue newly introduced. Three minor suggestions logged as non-shipping items: (a) make `CompanySelectionActivity` strict on `assignedCompanyId` for the Quotation path (reject instead of falling back to `selectedCompanyId`); (b) `GetFinalizedByAppraisalIdAsync` could order by a dedicated `FinalizedAt` column instead of `RequestDate`; (c) `CompanyAssignedIntegrationEventHandler` still reads `AppraisalFees` via DbContext — an `IAppraisalFeeRepository` would keep it repo-only. None are blockers.

Feature is ship-ready pending commit + PR (no commits yet per instruction).
