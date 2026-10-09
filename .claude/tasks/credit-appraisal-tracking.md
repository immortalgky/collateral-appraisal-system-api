# Credit-officer view of appraisal work

## Context

Today every signed-in user sees the same Appraisal Search page and the same appraisal
workspace. The appraisal department asked for credit (สินเชื่อ) users to be limited to:

1. where an appraisal is in the pipeline — and who is holding it,
2. the appraised value and the documents **only once the price is approved**,
3. the two documents they actually file: Appraisal Summary and Appraisal Report,
   with every other document in the folder also openable.

Design settled over 10 mock rounds; the approved direction is direction **F** in
`../../../collateral-appraisal-system-app/docs/poc/credit-collateral-brief-mock.html`
(full-bleed hero + sticky vertical progress spine).

## Why this is not only a UI change

No endpoint on the appraisal read path carries a permission policy. `GET /appraisals`
already returns `AppraisalValue` on every row, `GET /appraisals/{id}/documents` has no
`RequireAuthorization()` at all, and `GET /appraisals/{id}` has neither policy nor
scoping. Hiding columns client-side leaks through DevTools, so the field masking has to
happen in the handlers.

## Decisions taken (flag now if any is wrong)

| # | Decision | Why |
|---|---|---|
| 1 | "Approved" = **appraisal approved by committee** (`Appraisal.Status = Completed`, `CompletedAt` set by `MarkApprovedByCommittee`) | CAS has no loan-approval signal at all — grep for `LoanApprov\|ApplicationApprov\|CreditApprov` returns 0 hits, and LOS integration is outbound only. Option (b) needs a new inbound LOS contract. |
| 2 | **Reuse the existing list page**, mask fields server-side; build only the detail screen new | Keeps filters / quick views / saved searches. One shared masking helper is a smaller audit surface than a parallel list endpoint. |
| 3 | Credit users see **every appraisal** (no `RequestedBy` predicate) | User's call: "เห็นได้ทุกใบไปก่อน". Masking is therefore the only remaining control. |
| 4 | **No new roles.** Grant to the roles that already exist: `Inquiry` (1,042 users) and `Report` (203). "Inquiry + Report" is a user holding both — Identity allows several roles per user. | ⚠ Corrected after the fact: three roles named `Credit*` were invented and seeded before anyone checked `auth.AspNetRoles`. They have been removed from the seeder and deleted from the dev database. **Both existing roles already hold `APPRAISAL_VIEW`** — which is exactly why every user sees the same appraisal search today — so the grant alone changes nothing; see the revoke note below. |
| 5 | **Export is disabled** for tracking-only users | "See every appraisal" + Excel export of 10,000 rows = the bank's collateral book leaves in one click. |

## Scope

New permission `APPRAISAL_TRACKING_VIEW`, a top-level menu, a new read endpoint for the
brief screen, field masking on the four existing read paths, a permission-aware
destination for search results, and the new frontend screen.

**Not in scope** (pre-existing debt, listed in Backlog): the report and document
download endpoints.

---

## Backend

### 1. Permission + policy

- `Modules/Auth/Auth/Infrastructure/Seed/AuthDataSeed.cs` — add to the `seedPermissions`
  tuple list (end of the list, `Appraisal` module):
  `("APPRAISAL_TRACKING_VIEW", "Track Appraisal Progress", "View appraisal progress, approved value and documents without access to the appraisal workspace", "Appraisal")`
- `Modules/Auth/Auth/AuthModule.cs` `AddPolicies()` — the endpoints must accept **either**
  permission, so add an any-of helper next to `AddMonitoringAnyPolicy` (which already does
  exactly this) and register:
  `.AddUserPermissionAnyPolicy("appraisal.browse", ["APPRAISAL_VIEW", "APPRAISAL_TRACKING_VIEW"])`
  (named `browse`, not `read` — `appraisal.read` is already an OAuth **scope** string in this
  same file, and reusing it would make every grep ambiguous)
  Do **not** use `AddUserPermissionPrefixPolicy("APPRAISAL_")` — it would also match
  `APPRAISAL_360_VIEW` etc., which `RequestMaker` already holds.

### 2. Field masking — one helper, four call sites

New `Modules/Appraisal/Appraisal/Application/Features/Shared/AppraisalFieldScope.cs`,
sitting beside the existing `AppraisalAccessScope`:

```csharp
public static bool IsTrackingOnly(ICurrentUserService user) =>
    !user.HasPermission("APPRAISAL_VIEW") && user.HasPermission("APPRAISAL_TRACKING_VIEW");

// Null out what credit must not see. Value clears until the price is approved;
// the appraiser/company/SLA fields clear unconditionally.
public static AppraisalDto Mask(AppraisalDto dto) => dto with { ... };
```

Cleared always: `CompanyName`, `CompanyNameLocal`, `AssigneeUserId`,
`InternalAppraiserId/Name`, `ExternalAppraiserId/Name`, `SLAHours`, `SLADueDate`,
`SLAStatus`, `SLABusinessDays`, `ElapsedHours`, `RemainingHours`.
Cleared unless `Status == "Completed"`: `AppraisalValue`.

Apply in (all four already call `AppraisalAccessScope`, so the injection point exists):

- `.../GetAppraisals/GetAppraisalsQueryHandler.cs`
- `.../ExportAppraisals/ExportAppraisalsQueryHandler.cs` — and refuse outright for
  tracking-only (decision 5)
- `.../QuickSearch/QuickSearchQueryHandler.cs`
- `.../GetAppraisalById/GetAppraisalByIdQueryHandler.cs`

### 3. Search results must land on the right screen

`QuickSearchQueryHandler.cs:177` builds `NavigateTo` server-side on purpose (see the
comment on `SearchAppraisal`). Inject `ICurrentUserService` and emit
`/appraisals/{id}/brief` for tracking-only callers. The frontend needs no change — it
navigates to whatever the server returns (`useGlobalSearch.ts:145`).

### 4. New endpoint for the brief screen

`GET /appraisals/{appraisalId:guid}/brief` → `.RequireAuthorization("appraisal.browse")`.
One Dapper handler assembling what the screen needs, and the **only** place the release
rule is written:

- header: customer, loan application number, facility limit, property type, appraisal
  number, status, requestor + branch
- values: `AppraisalValue`, forced-sale, insurance, government price — all `null`
  unless approved
- assets: per-property title / area / location, value `null` unless approved
- progress: reuse `GetAppraisalWorkflowProgressQueryHandler`'s shape but **phase + dates
  + assignee display name only** — no SLA, no internal remarks
- documents: `parameter.DocumentTypes` joined to `appraisal.AppraisalDocuments`, with
  `D043`/`D001` flagged as the two primaries; empty list unless approved

### 5. Policies on the endpoints credit can reach

⚠ **Changed during implementation.** The original plan was to put `appraisal.browse` on
`GET /appraisals`, `/appraisals/{id}`, `/search` and the documents endpoint. That would
have been a regression: `src/features/request/components/SearchAppraisalModal.tsx` calls
`GET /appraisals`, and `RequestMaker` — who uses it to pick a previous appraisal — does
**not** hold `APPRAISAL_VIEW`. Gating those endpoints would have locked request makers
out of their own screen.

So the control is the **masking, not the policy**. Decision 3 already says credit may see
every row, so the row set was never the sensitive part — only the columns, and
`AppraisalFieldScope` handles those regardless of which policy let the caller in.

What actually changed:

- `appraisal.browse` is applied **only to the new brief endpoint**.
- `/appraisals/export` refuses tracking-only callers inside the handler
  (`UnauthorizedAccessException` → 403 via the global handler; the repo has no
  `ForbiddenException`).
- `/appraisals/{id}/documents` had **no authorization call whatsoever**; it is now
  explicitly `.RequireAuthorization()` (login-only, no policy) — a strict improvement that
  breaks nobody.

---

## Frontend (`collateral-appraisal-system-app`)

1. `src/app/router.tsx:799` — wrap the `/appraisals/:appraisalId` tree in
   `<RoleProtectedRoute allowedRoles={[]} requiredPermission="APPRAISAL_VIEW" />` so a
   typed URL or an old bookmark redirects instead of rendering the workspace.
2. New route `/appraisals/:appraisalId/brief` **outside** that guarded tree, with its own
   minimal layout.
3. New `src/features/creditBrief/` — page, `useGetAppraisalBrief` hook, and the
   components ported from direction F of the mock: hero, vertical `ProgressSpine`,
   `HolderCard`, `ValueMeter`, `AssetList`, `DocumentList` (two primaries + the quiet
   "เอกสารอื่นในแฟ้มประเมิน" list).
4. `src/features/appraisal/components/summary/ActivityTrackingContent.tsx:160` — the
   "View details" link resolves to `/brief` or the workspace via `useHasPermission('APPRAISAL_VIEW')`.
5. `AppraisalListPage.tsx` — for tracking-only users drop the SLA / priority / assignment
   / company / banking-segment columns and their filters, and hide the Export button.
   Cosmetic only; the server is the real gate.
6. i18n `th` + `en` for the new screen.

---

## ⚠ The revoke is the part that actually switches users over

`AppraisalFieldScope.IsTrackingOnly` is `!APPRAISAL_VIEW && APPRAISAL_TRACKING_VIEW` — the
fuller permission deliberately wins, so an internal user who also happens to hold the tracking
code is never degraded. The consequence is that granting `APPRAISAL_TRACKING_VIEW` to `Inquiry`
and `Report` **changes nothing at all** while those roles keep `APPRAISAL_VIEW`: no column is
masked, export is not refused, and the route guard still lets them into the workspace.

Switching them over means **removing `APPRAISAL_VIEW`** (plus the menu-only
`APPRAISAL_*_VIEW` section codes) from both roles — 1,245 accounts on the dev database. That is
a journalled migration sequenced immediately after the grant:

`Database/Migration/Scripts/20260914090100_Revoke_AppraisalWorkspaceFromCreditRoles.sql`

It was first written into `Database/Scripts/Maintenance/` so a human would have to run it
deliberately. That was wrong: `DatabaseMigrator.FilterMigrationScripts` only executes
`.Migration.Scripts.`, and the repeatable pass only `.Scripts.Views|StoredProcedures|Functions.`
— a file under `Scripts/Maintenance` is embedded in the assembly and executed by nothing. On
UAT and production it would have been skipped in silence, shipping a feature that changes
nothing. Automatic and journalled beats depending on someone remembering.

It refuses to run before the grant, prints the affected user counts first, and prints the exact
INSERT to undo it. No explicit transaction — the migrator wraps the whole upgrade in one.

## Database scripts (`Database/Migration/Scripts/`)

Seeders are create-only and do not run outside Development, so UAT/PROD needs SQL.
One file, named to match the existing convention
(`20260913120000_SeedData_HangfireDashboardMenu.sql`):

`2026MMDDHHMMSS_SeedData_AppraisalTrackingPermission.sql`

- insert `APPRAISAL_TRACKING_VIEW` into `auth.Permissions` if absent
- insert the top-level menu item `main.appraisal-tracking` → `/appraisals/tracking`
  with `ViewPermissionCode = APPRAISAL_TRACKING_VIEW`
  (**top level, not a child of `main.appraisal`** — `GetMyMenuQueryHandler` does
  `if (!isVisible) continue;` *before* recursing, so a child under a hidden parent never
  renders)
- link the permission to `RequestMaker` in `auth.RolePermission` if absent

---

## Verification

1. `dotnet build` clean, then `dotnet run --project Database/Database.csproj migrate`.
2. As a user holding only `APPRAISAL_TRACKING_VIEW`:
   - the Appraisal menu is gone, "ติดตามงานประเมิน" is present
   - `GET /appraisals` returns `appraisalValue: null` on unapproved rows **in the JSON**,
     and never returns `companyName` / SLA fields
   - `/appraisals/{id}` and `/appraisals/search` typed directly redirect to `/`
   - `/appraisals/export` is refused
   - global search lands on `/brief`
3. As a user holding `APPRAISAL_VIEW`: everything behaves exactly as before (regression).
4. On an approved appraisal the brief shows the value and all documents; on an in-flight
   one both are locked and the progress spine still names the current holder.

---

## Backlog — pre-existing holes this work touches but does not close

- `GET /reports/{reportTypeKey}/{entityId}` is login-only with no per-appraisal check, so
  any authenticated user can render any appraisal's book by number.
- `GET /documents/{id}/download` is `.AllowAnonymous()` — security by unguessable Guid.
- `APPRAISAL_*_VIEW` section permissions gate menus only; they gate no API.
- `GetAppraisalMapPins` and `GetPreviousAppraisalChain` also read appraisal data and are
  not covered by decision 2's masking.

---

## Review

Backend and frontend implemented; nothing committed.

### What shipped

**Backend** (builds clean, 39 projects, 0 errors)

- `APPRAISAL_TRACKING_VIEW` permission + roles `CreditInquiry` / `CreditReport` /
  `CreditInquiryReport`, all holding the one permission (`AuthDataSeed.cs`).
- Policy `appraisal.browse` accepting either permission. The existing
  `AddMonitoringAnyPolicy` helper was renamed `AddUserPermissionAnyPolicy` — it was never
  Monitoring-specific and an Appraisal policy now needs the same shape.
- `AppraisalFieldScope` — the one place the column rule lives — wired into the list,
  single-read, export and quick-search handlers.
- `GET /appraisals/{id}/brief`: a purpose-built read where the withheld fields do not exist
  in the shape at all, so there is nothing to leak. Documents are withheld **whole** while
  locked, because a `documentId` is a working download link on its own.
- Quick search emits `/appraisals/{id}/brief` for tracking-only callers; the client needed
  no change, since the destination has always been server-built.
- Top-level menu node + an idempotent SQL script for UAT/production.

**Frontend** (typecheck and eslint clean on the changed files)

- `src/features/creditBrief/` — `AppraisalBriefPage` plus `ProgressSpine`, `ValueMeter`,
  `DocumentList`.
- Routes: `/appraisals/tracking` (reuses `AppraisalListPage`) and
  `/appraisals/:appraisalId/brief`; the workspace tree is now guarded on `APPRAISAL_VIEW`.
- `RoleProtectedRoute` gained `children` support. It only rendered `<Outlet />`, so wrapping
  a single element in it silently rendered nothing — the guard would have appeared to work
  while showing a blank page.
- `ActivityTrackingContent`'s detail link and `AppraisalListPage`'s columns/filters/export
  now follow the permission.

### Two things the database contradicted

- **Per-asset value was dropped.** `AppraisalProperties.SellingPrice` is populated on 6 of
  105,660 rows — pricing hangs off a property *group*, not a property. A per-item column
  could only ever have been blank, so the released total is the only money on the asset list.
  Grouped values are a follow-up.
- **`RequestCustomers` has no id-card column** (only `Id, Name, ContactNumber, RequestId`),
  and land area lives on `LandTitles` as `AreaRai/AreaNgan/AreaSquareWa` keyed by
  `LandAppraisalDetailId`. All three statements in the brief query were executed against the
  dev database before being called done — a clean C# build says nothing about column names.

### Plan corrections made during the work

- Policies were **not** added to `GET /appraisals` / `{id}` / `/search`: `SearchAppraisalModal`
  in the request feature calls the list, and `RequestMaker` has no `APPRAISAL_VIEW`. Masking
  is the control; see section 5.
- The policy is `appraisal.browse`, not `appraisal.read` — the latter is already an OAuth
  scope string in `AuthModule.cs`.

### Not done

- Automated tests (none requested).
- i18n: the brief page carries Thai strings inline rather than going through `react-i18next`.
  Fine for a screen with one audience, but it is the odd one out in this codebase.

### Operational note

`MenuTreeCache` has no TTL, so the API must be restarted after the SQL script runs, and the
credit users must re-login — permissions are a token claim.

## Backlog — carried out of this change

- **`GET /appraisals/{id}` is not company-scoped.** An external firm's user can read any appraisal
  by id (facility limit, appraised value, appraiser, SLA); the endpoint has only the global login
  fallback. Neither is `GET /appraisals/{id}/brief` — a row scope was built there and REMOVED on
  2026-09-15 after five rewrites, four of which locked out entitled firms. Do not rebuild it from
  that code: it is gone, and the reasons are in the handler's XML doc and in the round-9/10/11
  reviews below. The short version for whoever picks this up:

  * A firm is linked to a job in three places, at three different times — the quotation fan-out task
    (company column set), the negotiation pool task (company NULL, company encoded in the assignee
    name as `:Team_<companyId>`), and `AppraisalAssignments` (only after admin-finalize). No single
    predicate spans them, and `ClaimTaskCommandHandler` / `TaskCompletedDomainEventHandler` rewrite
    the assignee to a bare username mid-flight, erasing the only evidence the middle case has.
  * `PoolTaskAccess` is NOT the rule to reuse. Its `AssigneeCompanyId IS NULL` arm is deliberately
    open — correct for "which pool tasks may I claim", wrong for "may I read this record" — and
    group names are not company-specific (`IUserGroupService` returns names; every firm has an
    "ExtAdmin").
  * Doing it one endpoint at a time buys nothing while the neighbours are open. It has to be
    task-level authorisation applied across all 13 by-id call sites in one change.

  What already works, and should not be re-solved: cross-company separation at the DISCOVERY layer —
  `AppraisalFilterBuilder.cs:54` and `QuickSearchQueryHandler.cs:87` scope the list, quick search and
  export by `AssigneeCompanyId`.

- **`GET /appraisals/{id}/documents` is not company-scoped** either. It now withholds document ids
  from tracking-only callers on an unreleased appraisal, but an external firm holds `APPRAISAL_VIEW`
  and can still read another firm's checklist by id.
- **`GET /documents/{id}/download` is `.AllowAnonymous()`** and `GET /reports/{key}/{entityId}` has
  no per-appraisal authorisation. Both predate this work and both are what make a leaked
  `documentId` immediately useful.


## Review — review round 8 (2026-09-15)

Six findings on this change, plus one pre-existing hole judged in scope because it is a hole in the
release rule itself.

- **The task arm of the brief's row scope was wrong** (the fix from round 7 did not work). It
  matched `AssignedTo = @UserCode`, which only ever matches `AssignedType='1'`. Replaced with
  `IPoolTaskClauseService` — the module's own rule, already unit-tested, and the only expression
  that covers all three writers (`TaskAssignedEventHandler`, `ApprovalTasksAssignedEventHandler`,
  `FanOutTasksAssignedEventHandler`). It also carries the company gate, which closes the second
  finding: the old arm was company-blind and granted a departed user permanent access.
- **It was also looking under the wrong correlation id.** Quotation tasks are filed under the
  QUOTATION REQUEST id (`QuotationStartedIntegrationEventConsumer`), not the request id, so the
  invited firm's task was not reachable from `a.RequestId` at all. `TaskCorrelationSql` now spans
  both, joining through `QuotationRequestAppraisals`.
  Verified against a real fixture: invited firm → 1 row, another firm → 0, internal → 1,
  another firm without the group → 0.
- **Passing an `UnauthorizedAccessException`'s message to the client leaked filesystem paths** —
  `System.IO` throws that same type with the absolute path in the message, and document upload and
  download are on request paths. New `ForbiddenException` carries caller-facing refusals;
  `UnauthorizedAccessException` is mute again.
- **The panel claimed "the committee has not approved the price yet" when the brief simply failed.**
  `brief?.isReleased` being falsy is absence, not a verdict. The header now has a third state.
- **`CurrentHolder` now honours the rule its comment describes** (nothing on a finished or
  cancelled appraisal) — a pending task is not proof the work is live, since the last task of a
  workflow is only archived if something archives it.

### Deliberately NOT fixed — out of scope (2026-09-15, user's call)

- **`GET /appraisals/{id}/decision-summary` carries no policy**, so any authenticated caller can
  read `TotalAppraisalPrice` / `ForceSellingPrice` for any id — the same figures the brief
  withholds until the committee approves. A gate plus the release rule was written and **reverted**:
  this endpoint does not serve the slide-over. It serves the 360 page, the Decision Summary page,
  VoteDialog and history search's pin drawer, and this change is scoped to the search page's
  slide-over alone. Closing it is a separate piece of work with its own blast radius —
  history search reads it for ~1,250 credit users who keep `HISTORY_SEARCH_VIEW` while losing
  `APPRAISAL_VIEW`, so the naive gate takes the prices off a screen they are still entitled to.

### Still open, not fixed

- `/appraisals/{id}/documents` unscoped; `/documents/{id}/download` anonymous (backlog above).
- The revoke script silently no-ops if `Inquiry` / `Report` were renamed, and journals as applied.
- `auth.UserPermissions` is not walked by the compensating grant, so a per-user `APPRAISAL_VIEW`
  holder loses the menu route.
- `vw_AppraisalDetail`'s `TOP 1` picks have no tiebreakers.


## Review — review round 9 (2026-09-15)

Three of the findings were regressions introduced by round 8's own fix.

- **The pool-task rule was the wrong rule for a read.** `PoolTaskAccess`'s company gate is
  `(AssigneeCompanyId IS NULL OR = @caller)`, and the NULL arm is deliberate for "which pool tasks
  may I claim". Used for "may I read this appraisal" it was a hole: `TaskAssignedEventHandler`
  writes no company at all, and `IUserGroupService` returns group NAMES, so every firm's "ExtAdmin"
  collapses to one candidate — any firm's admin could read the brief of an appraisal another firm
  won. `TaskCompanySql` now requires `t.AssigneeCompanyId = @EnforcedTaskCompanyId` on both task
  arms. The invited-to-quote case still passes because the fan-out stamps the company on every row.
  Re-verified: invited firm 1, another firm in the same group 0, internal 1, NULL-company row for a
  stranger firm 0.
- **The release rule was applied to everyone, not just credit.** `ActivityTrackingContent` is also
  mounted by `ActivityTrackingPage` as a tab inside the appraisal workspace and the task tree, so an
  internal appraiser opening it on in-progress work was told the committee had not approved the
  price. `released` is now `!IsTrackingOnly(caller) || approved`; `CurrentHolder` keeps following the
  appraisal's own state (`approved || cancelled`), not the caller's disclosure level.
- **The workspace route guard blocked more than credit.** An allow-list on `APPRAISAL_VIEW` bounced
  `RequestMaker` / `RequestChecker`, who hold no appraisal permission and reach `/appraisals/{id}`
  from `PoolTaskListPage` when a task is locked by someone else. `RoleProtectedRoute` gained
  `deniedPermission`, and the route now denies the tracking-only audience — mirroring the server's
  `IsTrackingOnly` — instead of allow-listing one code on a path that previously had no guard.

Also: the export refusal's message now reaches the toast (`apiError.detail`), the status badge reads
the brief's stored status rather than `vw_AppraisalDetail`'s derived CASE, `totalArea` rounds before
carrying (99.97 wa was printing as "0 งาน 100 วา"), and the edit to the orphaned
`WorkflowProgressTrack.tsx` was reverted.

### Deliberately NOT changed

- **The IntAdmin roster stays visible to external callers.** Flagged as a disclosure; it is contact
  information, and an external valuation firm has to be able to reach the appraisal desk.
  (User's call, 2026-09-15.)
- The menu node's `SortOrder` differs between the SQL script (last) and `MenuSeedData` (between
  request and quotation) — the known menu-SortOrder-drift pattern, cosmetic, left alone.


## Review — review round 10 (2026-09-15)

Two of the three findings were, again, regressions from the previous round's fix. Both came from the
same habit: tightening a predicate without enumerating who it was already letting through.

- **The blanket company equality locked out the quotation NEGOTIATION round.** Round 9 required
  `t.AssigneeCompanyId = @caller` on every task row, on the reasoning that a NULL company means no
  company gate. That is only true for a BARE group name. `ext-respond-negotiation` is a pool
  activity with `teamIdVariable: tentativeWinnerCompanyId`, so `PoolAssigneeSelector` writes
  `"ExtAdmin:Team_<companyId>"` — company-specific in the NAME — through `TaskAssignedEventHandler`,
  which leaves the column NULL. The tentative winner has no `AppraisalAssignments` row yet (that is
  created later at admin-finalize), so the task is its only link and it got 404 on the appraisal it
  was actively negotiating. `TaskCompanySql` now accepts either the column or a team-qualified
  assignee, and `PoolTaskClause` gained `TeamId` so the pattern can be built.
  Truth table verified in SQL: fan-out row my company PASS / other company deny; team-qualified my
  company PASS / other company deny; bare group deny; direct user task deny. Full query against the
  real fixture: invited firm 1, other firm 0, internal 1.
- **`released` lost the `cancelled` term.** `!IsTrackingOnly || approved` returned IsReleased=true
  for a cancelled appraisal to every internal caller, and the panel only consults `isCancelled`
  inside the locked branch — so the cancelled notice and DocumentList's cancelled state became
  unreachable and the three-figure block rendered over numbers nobody approved. Now
  `approved || (!IsTrackingOnly && !cancelled)`.
- The export toast trusts the server's message only on **403** — the catch-all arm of
  CustomExceptionHandler puts `exception.Message` verbatim into `detail`, so a SqlException from the
  workbook build would have put table and column names in front of a credit officer.
- The "View details" link now mirrors the route guard (deny tracking-only) instead of allow-listing
  `APPRAISAL_VIEW`, which had hidden it from the same RequestMaker / RequestChecker the guard admits.

### Known gap, recorded not closed

- **The `/tasks/:taskId` route tree mirrors the whole appraisal workspace behind login only.** The
  guard added to `/appraisals/:appraisalId` is therefore a front door, not a wall: a caller holding
  a task id can still render administration / property / documents / summary / 360 there.
  `GetTaskByIdQueryHandler` does not 403 a non-owner (it returns `IsOwner = false`) and
  `GetAppraisalByIdQueryHandler` is unscoped, so nothing behind it refuses either. Not an active
  leak — the id is a v7 GUID — and deliberately not closed here: that tree serves people whose
  permission sets were never surveyed, and the last two rounds were spent undoing exactly that kind
  of unsurveyed tightening. This is the same two-route-trees divergence the repo already has a
  standing note about.


## Review — review round 11, and the decision that followed (2026-09-15)

Round 11 found three more ways the brief's row scope locked out someone entitled, and none of them
was a leak:

- `ClaimTaskCommandHandler` and `TaskCompletedDomainEventHandler` both rewrite `AssignedTo` to a bare
  username while leaving `AssigneeCompanyId` NULL, so the team arm died the moment the tentative
  winner claimed or answered the negotiation task — the same 404 round 10 had just fixed, one click
  later.
- `CompanyTeamService.GetTeamForUserAsync` reads `auth.TeamMembers` FIRST, so an external user who is
  also a member of an internal team gets that team's GUID as `TeamId` and can never match their own
  firm's rows. The doc comments asserting "TeamId is the CompanyId" were only true by accident.
- At the checker stage of a quotation, `AssignedTo` becomes `ExtAppraisalChecker:Team_<co>`, so the
  ExtAdmin who wrote the quotation was 404'd from the moment they submitted it to their own checker.

### Decision: the row scope was REMOVED

Five attempts, four of which locked out entitled users. The predicate was chasing a link that moves
— invitation (fan-out task, company set), negotiation (team-qualified pool task, company NULL),
assignment (AppraisalAssignments) — with two writers that rewrite the assignee in between.

It also protected nothing. Cross-company separation in this system is at the DISCOVERY layer:
`AppraisalFilterBuilder.cs:54` and `QuickSearchQueryHandler.cs:87` force
`AssigneeCompanyId = @ScopedCompanyId`, so an external firm never sees another firm's appraisals in
the list, quick search or export. Every by-id read — `/appraisals/{id}`, the workspace tabs,
`/appraisals/{id}/documents` — is unscoped and already returns the facility limit, the appraised
value and the same document ids to any `APPRAISAL_VIEW` holder, which all three external roles hold.
Scoping one door in an unlocked corridor cost real users access and closed nothing.

`GET /appraisals/{id}/brief` now reads like its neighbours: `appraisal.browse` policy plus the
release rule, which is the control the feature actually asked for. `PoolTaskClause`'s `TeamId`
addition was reverted with it — the cross-module contract is untouched.

**What this hands to the backlog** (already listed there): proper per-appraisal authorisation for
external callers has to be done once, across all 13 by-id call sites, as task-level authorisation —
not a company column, and not one endpoint at a time.


## Handed to the workflow owner — pool tasks stored as AssignedType '1' (2026-09-16)

Not fixed here. The user is fixing it at the writer; this panel trusts `AssignedType` and nothing
else. Recorded so the trace is not lost.

**Symptom.** `workflow.PendingTasks` / `CompletedTasks` carry rows whose `AssignedTo` is a GROUP
("IntAdmin", "ExtAdmin:Team_<guid>", "SYSTEM", a bare guid) while `AssignedType` says '1'. The dev
database has 101 such completed rows and 2 pending. Any reader that trusts the type then renders a
group as an individual.

**The three code points, all verified by reading the writers:**

1. `TaskActivity.cs:106` and `FanOutTaskActivity.cs:432` —
   `assignedType = metadata["AssignedType"] ?? "1"`. Only 2 of the 10 `IAssigneeSelector`
   implementations put that key in their metadata (`PoolAssigneeSelector`, `VariableAssigneeSelector`),
   so everything else defaults to "person".
2. `PreviousOwnerAssigneeSelector` returns `WorkflowActivityExecutions.CompletedBy` verbatim and
   attaches no `AssignedType` — and `CompletedBy` is itself contaminated upstream:
   `FanOutTaskActivity.cs:214` (`CompletedBy = completedBy ?? companyTask.AssignedTo`) and
   `TaskAssignedEventHandler.cs:28,66` (`notification.CompletedBy ?? existingTask?.AssignedTo`) both
   fall back to the task's own assignee, which for a pool task is the group name.
3. `TaskCompletedDomainEventHandler.cs:49` — `pendingTask.Reassign(notification.CompletedBy, "1")`
   writes whatever came out of (2) as an individual.

**The rule already exists in the codebase**, at `TaskCompletedDomainEventHandler.cs:57-60`:
"Never fall back to AssignedTo for a pool task (AssignedType '2') — that is the assignee GROUP, not
a person, and it leaks into downstream notifications/emails." It is applied when that handler
resolves its own `completedBy`, but not at the two fallbacks in (2), which is where the leak starts.

**Two workarounds were tried in this panel and BOTH were removed:**
- Testing for a ':Team_' suffix — misses a bare group name, which `PoolAssigneeSelector` emits
  whenever no team was resolved.
- Testing whether a matching `auth.AspNetUsers` row exists — strictly worse. A group name can equal
  a username: the dev database has 13 live pool rows assigned to "Admin" while `auth.AspNetUsers`
  holds "admin", and the collation is case-insensitive, so the brief named a person who did not hold
  the work and published the `IsSystem = 1` break-glass account's contact details.


## Activity-log query now carries the task's company (2026-09-16)

`GetAppraisalWorkflowProgressQueryHandler` never selected `AssigneeCompanyId`, and `ActivityLogRow`
had nowhere to put it, so the only company it could report was the assignee's own employer — which
a POOL row cannot have, because it has no assignee. The brief reads the task instead, so after its
`pc` join was added the two blocks on one screen disagreed: the rail named the group ("ExtAdmin")
while the holder card named the valuation firm.

Fixed at the query: both UNION arms now select `AssigneeCompanyId`, and one `LEFT JOIN auth.Companies`
outside the derived table resolves the firm from either the column or the `:Team_<teamId>` guid in
the assignee name — the same pair the brief resolves. The assignee's employer stays as the fallback
(`??=`) so nothing an individual's row used to report changes.

Verified against the dev database, all five row shapes: fan-out row with the column set → firm;
team-scoped pool name → firm; INTERNAL team id → null (correct — not a company); plain pool → null;
person → null (falls back to the employer).

This also removes the reason `ActivityTimeline.isExternalLeg` had to sniff `activityId.startsWith('ext-')`
— `companyName` is now populated on the pool rows it was working around. Left as is; it is a correct
belt-and-braces test, not a workaround that now misfires.


## Review — round 18 (2026-09-16)

Two findings were dismissed on the user's domain knowledge and then confirmed in code; the rest were
fixed.

**Not real — the company on a task cannot diverge from the holder's employer.** The review flagged
that `companyName` / `companyNameLocal` fall back to the assignee's employer independently, and that
`PendingTask.Reassign` never clears `AssigneeCompanyId`. Both assume work can cross a company
boundary. It cannot: `ClaimTaskCommandHandler` gates on `PoolTaskAccess.IsOwner(..., task.AssigneeCompanyId,
..., currentUser.CompanyId)`, whose company arm is `callerCompanyId == assigneeCompanyId`, and
`ReassignTaskCommandHandler` gates on `taskMonitorScope.IsTargetInScopeAsync("TASK_MONITOR_REASSIGN", ...)`
— the supervisor's own team (internal) or company (external) — plus the activity's candidate pool. A
firm's task is claimed and reassigned only within that firm, so both names resolve to one company and
a row cannot carry a stale one.

**The new `LEFT JOIN auth.Companies` on the activity log is inert today.** Neither arm can match
under `WHERE CorrelationId = @RequestId`: `AssigneeCompanyId` is written only by
`FanOutTasksAssignedEventHandler`, reached only from `FanOutTaskActivity`, which appears only in
`quotation-workflow.json` — and that workflow correlates on the QuotationRequestId, not the
appraisal's RequestId. The `:Team_` arm needs a pool with a resolved team, and `appraisal-workflow.json`'s
only pool (`appraisal-assignment`, group `IntAdmin`) has no team constraint. Kept anyway, by the
user's decision: it costs nothing (verified: 106,405 rows before and after, `Companies.Id` is the PK)
and it is what makes the rail agree with the holder card the day a workflow is configured with a
team-scoped pool on the appraisal side. The same is true of the brief's `pc` join — which is why the
"rail and card disagree" finding that prompted this work could not actually fire yet.

**Fixed:** the activity timeline drew a pool as a person (`IntAdmin` on `appraisal-assignment` —
105,235 rows, the most common entry in the log — as a teal "I" avatar) while the rail and the card
drew the same desk with the group icon; `HolderCard`'s header comment still claimed the phone was a
`tel:` link; `WorkflowProgressTrack` printed a raw group name on its person line.


## Review — round 20, regression-only (2026-09-16)

Scoped to three classes only — breaks something that works, locks out someone entitled, discloses
data. One finding, in the lockout class; classes 1 and 3 came back empty.

**History Search's "open report" link became a dead end for the credit roles.**
`PinDetailDrawer.tsx` rendered `<Link to={`/appraisals/${id}`} target="_blank">` with no permission
test at all. Inquiry keeps `HISTORY_SEARCH_VIEW` (the revoke script says so explicitly) but loses
`APPRAISAL_VIEW`, so the new route guard bounced them — in a NEW TAB, which lands on the dashboard
saying nothing. ~1,043 users. The revoke script's own blast-radius note claimed the workspace link
is "hidden rather than shown and then bounced"; that was true of the tracking panel only.

Fixed by extracting the rule instead of copying it a third time: `useCanOpenAppraisalWorkspace()`
(`src/features/appraisal/hooks/`) is now the single client-side definition, consumed by the tracking
panel and the pin drawer, and mirroring the route guard's `deniedPermission` predicate. Deny the
tracking-only audience rather than allow-list `APPRAISAL_VIEW` — the two are not complements, and
RequestMaker / RequestChecker hold neither code while the guard admits them.

### Status

Twenty review rounds. Last HIGH was round 16; rounds 18 and 19 each produced findings that proved
wrong on inspection. The remaining LOW items are recorded above and were deliberately not fixed.
The change is ready for manual testing.
