# Plan — Add table/grid (kanban) view toggle to the Pool Task page

**Goal:** Give the Pool Task page the same list ⇄ grid toggle My Tasks has, so supervisors can
triage the *unclaimed* backlog by grouping (SLA / activity / priority / purpose), not just scroll a table.

**Spans two repos:**
- Backend: `~/Developer/collateral-appraisal-system-api`
- Frontend: `~/Developer/collateral-appraisal-system-app`

---

## Key findings (why this is smaller than it looks)

- The grouping SP `workflow.sp_GetTaskGroupCounts` already takes `@AssignedType` where **`'2'` = pool**.
- `GET /tasks/pool` (`GetPoolTasksFilterRequest`) **already accepts every kanban scoping filter**
  (`Status, Priority, SlaStatus, ActivityId, Purpose, TaskStatusBucket, TaskName`) and already calls
  `sp_GetTaskList` with `assignedType:"2"`. → **Per-column pool fetch needs zero backend work.**
- Only missing backend piece: a **pool group-counts endpoint** (My-Tasks has `GET /tasks/me/group-counts`;
  pool has none).
- The real effort is a **pool-aware kanban card** that preserves the pool's lock / claim / take-out /
  edit-in-pool actions and the `PoolTaskLocked/Unlocked/Claimed` SignalR updates. A plain card would be a
  UX regression vs. the current table.

---

## Backend (small) — repo: collateral-appraisal-system-api

- [x] **1. Add `GetPoolTaskGroupCounts` feature** under
  `Modules/Workflow/Workflow/Tasks/Features/GetPoolTaskGroupCounts/`
  (Query + Handler + Endpoint), modeled on `GetMyTaskGroupCounts`.
  - Handler resolves pool assignees exactly like `GetPoolTasksQueryHandler`
    (`IUserGroupService.GetGroupsForUserAsync` → `ITeamService.GetTeamForUserAsync` →
    `PoolTaskAccess.BuildProcAccess`), then calls
    `TaskGroupCountsProcParams.Build(assignedType:"2", assignees, companyGate, callerCompanyId, filter, groupBy)`.
  - Reuse the same `AllowedGroupBy` guard (status/priority/purpose/activity/slaStatus).
  - Reuse the `GetPoolTasksFilterRequest` shape for filters so counts + column fetch stay in sync.
- [x] **2. Endpoint:** `GET /tasks/pool/group-counts` (mirror `GetMyTaskGroupCountsEndpoint` authz/route conventions).
- [x] **3. Wire missing pool route params:** `GetPoolTasksEndpoint` route binds only a subset — it omits
  `purpose` and `taskStatusBucket` (though `GetPoolTasksFilterRequest` already supports both). The
  status-grouped kanban scopes each lane by `taskStatusBucket` (and purpose grouping by `purpose`), so add
  both `[FromQuery]` params to the `/tasks/pool` route and pass them into the filter.
- [x] **3b. Verify** `/tasks/pool?taskStatusBucket=Overdue` returns the right rows.

## Frontend (the bulk) — repo: collateral-appraisal-system-app

- [x] **4. Scope the kanban data hooks** in `src/features/task/api.ts`:
  add a `scope: 'me' | 'pool'` param to `useTaskGroupCounts` and `useKanbanColumnTasks` that swaps
  `/tasks/me` → `/tasks/pool` and `/tasks/me/group-counts` → `/tasks/pool/group-counts`.
  (Keep default `'me'` so `TaskListingPage` is untouched.)
- [x] **5. Pool-aware kanban card** — the crux. **Decision: extend** `TaskKanbanBoard` / `TaskKanbanColumn`
  with a `scope` prop + a custom card renderer (avoids duplicating the column / infinite-scroll logic).
  The pool card renderer ports the pool row's lock indicator, **Claim / Take-out / Edit-in-pool** actions
  (all three required) and disabled/locked states from `PoolTaskListPage`.
- [x] **6. Wire real-time** — feed `PoolTaskLocked/Unlocked/Claimed` (`useWorkflowHub({ poolGroups })`)
  into the kanban card state, matching how the table updates lock state today. Locked cards must show
  "working by X" and disable claim.
- [x] **7. Add the toggle** to `PoolTaskListPage`: `viewMode: 'grid' | 'list'` state + the list/grid-2
  icon buttons (copy the block from `TaskListingPage`), render table for `list`, pool kanban for `grid`.
- [x] **8. Default grouping = `status`** → three lanes **Not Started / In Progress / Overdue**
  (the SP maps TaskStatus `Assigned`→`NotStarted`; lane order/labels already defined by
  `STATUS_ORDER` + `STATUS_LABELS` in `TaskKanbanBoard`). Grouping picker still offers
  slaStatus/activity/priority/purpose as alternatives.
- [x] **9. Persist view + grouping choice** per the existing pattern (localStorage key, e.g. `task-view-pool`),
  consistent with the `task-columns-pool` column-config keys already used.

## Verification

- [ ] **10.** Backend builds; `GET /tasks/pool/group-counts?GroupBy=slaStatus` returns non-empty lanes for a
  user with pool groups; counts match table row counts under the same filters.
- [ ] **11.** FE: toggle switches views; grid lanes populate; claim/lock/take-out work from a card; a lock
  event from another user updates the card live; `tsc -b` shows no *new* errors for touched files
  (baseline has ~500 pre-existing).
- [ ] **12.** My Tasks page unaffected (regression check on the shared hooks).

## Decisions (confirmed)

1. **Card vs. reuse:** ✅ Extend `TaskKanbanColumn` with a `scope` prop + card renderer (no separate board).
2. **Card actions:** ✅ Claim + Take-out + **Edit-in-pool** all required on the grid card.
3. **Default grouping:** ✅ `status` — lanes **Not Started / In Progress / Overdue**.

---

## Review

**Status: implemented, builds/type-checks clean. Runtime (live DB + SignalR + claim/lock) not yet exercised.**

### Backend (repo: collateral-appraisal-system-api) — `dotnet build` 0 errors
- **New** `Modules/Workflow/Workflow/Tasks/Features/GetPoolTaskGroupCounts/` (Query + Handler + Endpoint).
  Handler resolves pool access exactly like `GetPoolTasksQueryHandler` (`IUserGroupService` →
  `ITeamService` → `PoolTaskAccess.BuildProcAccess`), same early-return guards, calls
  `sp_GetTaskGroupCounts` with `@AssignedType='2'`. Reuses `TaskGroupCountDto` and
  `GetPoolTasksFilterRequest` (already `: ITaskListFilter`).
- **New route** `GET /tasks/pool/group-counts` (auto-discovered `ICarterModule`, `.RequireAuthorization()`).
- **Changed** `GetPoolTasksEndpoint.cs` — bound `purpose` + `taskStatusBucket` query params (filter record
  already supported them) so the status-bucket kanban columns can scope `/tasks/pool`.

### Frontend (repo: collateral-appraisal-system-app) — no new tsc errors in touched files
- **Changed** `api.ts` — `KanbanScope = 'me'|'pool'`; `useKanbanColumnTasks` + `useTaskGroupCounts` take a
  `scope` (default `'me'`), swap `/tasks/me…` ↔ `/tasks/pool…`, and put `scope` as the 2nd query-key
  element (`['kanban-column','pool',…]`) so caches never collide.
- **Changed** `TaskKanbanBoard.tsx` / `TaskKanbanColumn.tsx` — added optional `scope` + `renderCard`
  (default behavior unchanged → My Tasks untouched).
- **New** `PoolTaskKanbanCard.tsx` — pool card cloning `TaskKanbanCard`'s layout + lock pill
  ("You're editing"/"Working by {name}") and the pool actions.
- **New** `usePoolKanbanRealtime.ts` — 2nd `useWorkflowHub` subscription; **in-place patch** of
  `['kanban-column','pool',…]` for Lock/Unlock, **invalidate** kanban + group-counts for Claimed.
- **Changed** `PoolTaskListPage.tsx` — `viewMode`/`groupBy` state (localStorage `task-view-pool` /
  `task-groupby-pool`, defaults `list`/`status`), right-aligned toggle + group-by row, grid branch renders
  `<TaskKanbanBoard scope="pool" renderCard=… />`; calls `usePoolKanbanRealtime()`.

### Notable deviations / follow-ups
1. **Only 2 pool actions exist, not 3.** In the pool table "Take Out" *is* the claim (`useClaimTask`), and
   "Edit in pool" (`handleEditInPool`) is lock+navigate (relabels View/Continue by lock state). The card
   mirrors these two exactly — there is no separate third "Claim" button anywhere in the pool UI.
2. **Toggle placement — RESOLVED (follow-up).** The pool `viewMode`/`groupBy` were lifted into
   `TaskListingPage`, so the Pool-tab shared toolbar now mirrors the personal tab exactly: Group-by select
   (grid only) + list/board toggle next to Search + Filters, and the Columns dropdown is gated to list mode.
   `PoolTaskListPage` takes optional `viewMode?`/`groupBy?` props (falls back to internal localStorage state
   when standalone) and no longer renders an in-body toggle. Persistence keys unchanged
   (`task-view-pool` / `task-groupby-pool`). Personal tab untouched.
3. **Real-time is a 2nd hub subscription** alongside the table's existing one — safe because `appHub`
   group/event registries are `Set`-based/idempotent.

### Bug found & fixed during testing — infinite scroll (both My & Pool)
Symptom: a lane showed a huge total (e.g. Overdue 105,231) but only the first page (`KANBAN_PAGE_SIZE=15`)
loaded and scrolling fetched nothing more.
- **Not a count bug.** DB confirms 105,244 pool Overdue `PendingTasks` (load-test data); `sp_GetTaskList`'s
  cheap-browse path pages straight off `PendingTasks` with `COUNT(*) OVER()` = same total the group-counts
  lane shows, and returns real enriched rows per page. Counts are consistent and paging returns data.
- **Root cause (frontend, pre-existing latent):** `TaskKanbanColumn`'s `IntersectionObserver` used the
  default **viewport** root while the cards scroll inside the column's own `overflow-y-auto` container, and
  the sentinel was a zero-height div — so the observer never reliably saw it. Never surfaced before because
  real My-task lanes are < 1 page; the load-test pool is the first >1-page lane.
- **Fix** (`TaskKanbanColumn.tsx`, shared → fixes both boards): root the observer on the scroll container
  (`scrollRef`), add `rootMargin: '400px 0px'` to prefetch, and give the sentinel real height (`h-4`).

### Not yet verified (needs a running app)
- `GET /tasks/pool/group-counts` returns lanes for a real pooled user; counts match table under same filters.
- Claim/lock/take-out from a card; a lock event from another user updating a card live.
- `tsc -b` full pass (agent had no shell; I ran targeted `tsc --noEmit` → touched files clean).
- Visual QA of the board on the Pool tab.
