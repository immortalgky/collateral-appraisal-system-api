# Listing-Page Index & Query Performance Audit

**Date:** 2026-06-08
**Scope:** The 5 highest-traffic listing pages and the read-side queries behind them.
**Deployment context:** N=2 IIS app servers + 1 SQL Server, no read replica — an un-indexed
scan on a hot list endpoint is felt directly by users.

This audit cross-references each page's **search/filter columns, sort columns, and join
columns** against the indexes that actually exist, and flags structural query-performance
risks that no index can fix.

---

## TL;DR

| # | Finding | Severity | Action |
|---|---------|----------|--------|
| 1 | `workflow.CompletedTasks` had **no indexes**; quotation-list access control scans it by `CorrelationId` | **HIGH** | ✅ Added `IX_CompletedTasks_CorrelationId` (migration) |
| 2 | `vw_TaskList` joins `auth.AspNetUsers` by `UserName` (not the indexed `NormalizedUserName`) | **MEDIUM** | ✅ Added `IX_AspNetUsers_UserName` (migration) |
| 3 | `AppraisalProperties.PropertyType`, Land/Condo address columns, `PendingTasks.TaskStatus` | LOW | Documented; not built (marginal) |
| 4 | Leading-wildcard `LIKE '%…%'` search is non-sargable everywhere | Structural | Monitor; prefix/full-text if needed |
| 5 | `vw_TaskList` 4-branch UNION + per-page COUNT over the full view | Structural | Watch at scale; `/tasks/counts` already bypasses |
| 6 | Some monitoring handlers return un-paginated full result sets | Structural | Paginate if breach lists grow |

**Overall:** existing coverage is good. Only **two** real index gaps were found and both are now
addressed by migrations (applied by the DBA/user, not by this change).

---

## Page-by-page coverage matrix

Legend: ✅ indexed & sargable · ⚠️ partial / shape-limited · ❌ gap (fixed) · 🔵 inherent (non-index)

### 1. Appraisal List — `vw_AppraisalList`
Handler: `GetAppraisalsQueryHandler` + `AppraisalFilterBuilder`

| Concern | Column(s) | Table | Existing index | Verdict |
|---|---|---|---|---|
| filter Status / Priority / AppraisalType / SLAStatus | `a.Status` etc. | Appraisals | `IX_Appraisals_Status` (filtered) | ✅ |
| filter / sort RequestId join | `a.RequestId` | Appraisals | `IX_Appraisals_RequestId` | ✅ |
| latest active assignment APPLY | `AppraisalId, AssignedAt DESC` (filtered) | AppraisalAssignments | `IX_AppraisalAssignments_AppraisalId_AssignedAt_Active` | ✅ **now**. Until 2026-08-30 this was a `ROW_NUMBER()` derived table filtered by `rn = 1` from the outside, not an APPLY — the index could not be used at all, and this row read ✅ only because the audit took the shape on trust. See §Structural concerns #7 |
| company name join | `comp.Id = TRY_CAST(...)` | auth.Companies | PK (seek on right side) | ✅ |
| latest appointment APPLY | `AssignmentId, Status` | Appointments | `IX_Appointments_AssignmentId`, `_Status` | ✅ |
| value join | `va.AppraisalId` | ValuationAnalyses | `IX_ValuationAnalyses_AppraisalId` (unique) | ✅ |
| customer name | `c.Name`, `RequestId` | RequestCustomers | `IX_RequestCustomer_Name`, `_RequestId` | ✅ |
| filter Province / District / SubDistrict | `ld.Province` … | LandAppraisalDetails | none | ⚠️ location is picked by a correlated `OUTER APPLY … TOP 1` and the filter is applied after that pick, so an index on Province cannot drive the filter. A covering `(AppraisalPropertyId) INCLUDE (Province, District, SubDistrict)` was measured (2026-08-30) and halves the CPU of *sorting* by province (1,280 ms → 675 ms); not built, since the elapsed difference is ~50 ms |
| search AppraisalNumber/CustomerName/RequestNumber | LIKE `'%…%'` | multiple | n/a | 🔵 non-sargable |
| sort PropertyCount / Elapsed / RemainingHours | computed | — | n/a | 🔵 computed, cannot index |

### 2. Task List (me / pool) — `vw_TaskList`
Handlers: `GetMyTasksQueryHandler`, `GetPoolTasksQueryHandler` + `TaskListFilterBuilder`, `PoolTaskAccess`

| Concern | Column(s) | Table | Existing index | Verdict |
|---|---|---|---|---|
| my-tasks base (`AssignedType='1' + AssignedTo`) | AssignedType, AssignedTo, AssigneeCompanyId | PendingTasks | `IX_PendingTasks_AssignedType_AssignedTo_Company` (covering) | ✅ |
| pool-tasks base (`AssignedType='2'` + company/group gate) | same | PendingTasks | same covering index | ✅ |
| branch correlation (Quotation/Fee/DocFollowup/Normal) | `pt.CorrelationId` | PendingTasks | `IX_PendingTasks_CorrelationId_AssignedAt` | ✅ |
| latest active assignment APPLY | filtered (AppraisalId, AssignedAt DESC) | AppraisalAssignments | `IX_..._AssignedAt_Active` | ✅ |
| customer / requested-by enrichment | RequestId, Name | RequestCustomers | indexed | ✅ |
| **user display join** | `u.UserName = ISNULL(a.RequestedBy, r.Requestor)` and `qrm.UserName = …` | auth.AspNetUsers | Identity indexes only `NormalizedUserName` | **❌ → fixed** (`IX_AspNetUsers_UserName`) |
| search / sort | LIKE / computed | — | — | 🔵 |

### 3. Monitoring — `vw_MonitoringPendingTasks`
Handlers: Common monitoring queries (pending-internal/external/followups/evaluations/quotations)

| Concern | Column(s) | Table | Existing index | Verdict |
|---|---|---|---|---|
| hard filter `TaskStatus IN ('Assigned','InProgress')` | `pt.TaskStatus` | PendingTasks | none | ⚠️ acceptable — PendingTasks holds only the bounded active queue (completed rows move to CompletedTasks) |
| latest appraisal per request APPLY | `a2.RequestId, RequestedAt DESC` | Appraisals | `IX_Appraisals_RequestId` | ✅ |
| latest active assignment APPLY | filtered | AppraisalAssignments | `IX_..._AssignedAt_Active` | ✅ |
| user PIC join | `u.NormalizedUserName = UPPER(pt.AssignedTo)` | auth.AspNetUsers | Identity `NormalizedUserName` unique | ✅ (correct — uses normalized) |
| group PIC join | `g.Name = pt.AssignedTo` (IsDeleted=0) | auth.Groups | `IX_Groups_Name_Scope` (filtered) | ✅ |
| document-followup fallback | `df.FollowupWorkflowInstanceId` | DocumentFollowups | (FK) | ✅ |

> Note: `vw_TaskList` and `vw_MonitoringPendingTasks` resolve the same user-name lookup in
> **opposite** ways — monitoring already joins on `NormalizedUserName` (indexed), while the task
> list joins on raw `UserName`. The cleaner long-term fix for finding #2 is to realign the task
> view to `NormalizedUserName` and drop the new index — see §Alternatives.

### 4. History Search — `HistorySearchQueryHandler` (raw Dapper, spatial)

| Concern | Column(s) | Table | Existing index | Verdict |
|---|---|---|---|---|
| appraisal pins radius | `GeoPoint.STDistance(...)` | Land/Condo AppraisalDetails | `IX_LandAppraisalDetails_GeoPoint`, `IX_CondoAppraisalDetails_GeoPoint` (SPATIAL) | ✅ |
| market-comparable pins radius | `mc.GeoPoint.STDistance(...)` | MarketComparables | `IX_MarketComparables_GeoPoint` (SPATIAL) | ✅ |
| MC company scope (external) | `mc.CreatedByCompanyId` | MarketComparables | none (residual after spatial seek) | ⚠️ marginal |
| completed-only hard filter | `a.CompletedAt IS NOT NULL` | Appraisals | — | 🔵 high-selectivity-low, but bounded by geo/EXISTS prefilter |
| collateral-type filter | `ap4.PropertyType IN (...)` (EXISTS) | AppraisalProperties | `IX_AppraisalProperties_AppraisalId` only | ⚠️ see §Low-priority |
| title-deed / customer LIKE | `lt.TitleNumber`, `al.CustomerName` | LandTitles / view | — | 🔵 non-sargable |
| land-area range | `AreaRai*400 + AreaNgan*100 + AreaSquareWa` | LandTitles | — | 🔵 computed expression, non-sargable (bounded by prefilter) |

### 5. Quotations — `vw_QuotationList`
Handler: `GetQuotationsQueryHandler`

| Concern | Column(s) | Table | Existing index | Verdict |
|---|---|---|---|---|
| filter Status | `q.Status` | QuotationRequests | `IX_QuotationRequests_Status`, `_Status_CutOffTime` | ✅ |
| filter CutOffTime range | `q.CutOffTime` | QuotationRequests | `IX_QuotationRequests_CutOffTime` | ✅ |
| AppraisalId filter (EXISTS) | `qra.QuotationRequestId, AppraisalId` | QuotationRequestAppraisals | both FKs + composite PK | ✅ |
| **non-admin access (EXISTS)** | `ct.CorrelationId = q.Id` + user/company gate | **workflow.CompletedTasks** | **none** | **❌ → fixed** (`IX_CompletedTasks_CorrelationId`) |
| non-admin access (EXISTS) | `pt.CorrelationId = q.Id` + gate | workflow.PendingTasks | `IX_PendingTasks_CorrelationId_AssignedAt` | ✅ |
| sort RequestDate | `q.RequestDate` | QuotationRequests | (default sort) | ⚠️ acceptable |

---

## Fixes delivered (migration files — not applied)

Both follow the existing convention: EF `HasIndex` in the entity configuration is the source of
truth, and the migration is generated from it. **The DBA/user applies them** (`dotnet ef database
update`); this change does not touch any database.

### Fix 1 — `IX_CompletedTasks_CorrelationId` (HIGH)
- Config: `Modules/Workflow/Workflow/Data/Configurations/CompletedTaskConfiguration.cs`
- Migration: `Modules/Workflow/Workflow/Infrastructure/Migrations/*_AddCompletedTasksCorrelationIdIndex.cs`
- Definition: `CREATE INDEX IX_CompletedTasks_CorrelationId ON workflow.CompletedTasks (CorrelationId) INCLUDE (AssignedType, AssignedTo, AssigneeCompanyId)`
- Why: `CompletedTasks` is append-only and grows without bound. The quotation-list access-control
  `EXISTS` correlates on `CorrelationId` then filters on the gate columns — without this index it is
  a clustered-index scan per non-admin quotation-list load. INCLUDE makes the seek fully covered.
  Write cost is negligible (inserts only, no updates).

### Fix 2 — `IX_AspNetUsers_UserName` (MEDIUM)
- Config: `Modules/Auth/Auth/Infrastructure/Configurations/ApplicationUserConfiguration.cs`
- Migration: `Modules/Auth/Auth/Infrastructure/Migrations/*_AddAspNetUsersUserNameIndex.cs`
- Definition: `CREATE INDEX IX_AspNetUsers_UserName ON auth.AspNetUsers (UserName) INCLUDE (FirstName, LastName)`
- Why: `vw_TaskList` joins users by raw `UserName` twice per row; Identity only indexes
  `NormalizedUserName`, so these joins scan `AspNetUsers` while building the busiest page. INCLUDE
  covers the `CONCAT(FirstName,' ',LastName)` display projection. Non-unique (no conflict with the
  existing unique `UserNameIndex` on `NormalizedUserName`).

---

## Fixes delivered 2026-08-30 — global-search index gaps (11 indexes)

**Context:** `GET /search` (the navbar quick-search) is being rebuilt so that every result resolves
to an appraisal, and so that it searches the columns users actually type: request number, LOS
application number, title deed, land parcel, condo/room, licence plate, project/village name, owner,
customer, contact person and requestor. The rebuilt predicate is **prefix-only** (`term%`, matching
`TaskListFilterBuilder.BuildSearchPattern`), which is what makes these indexes seekable at all —
the old handler used `%term%` on every column and could not have used any of them.

This PR ships **only the indexes**, so the DBA can schedule them independently of the query change.
Shipping the query first would make search *slower* than today, so this must land first.

**Measured on the local dev database — 105,579 `RequestTitles`, 105,536 `Requests`,
105,542 `RequestCustomers`, 105,519 `RequestDetails`.**

| Index | Table | Column | Filtered | Logical reads before → after |
|---|---|---|---|---|
| `IX_RequestTitle_OwnerName` | RequestTitles | `OwnerName` INCLUDE `RequestId` | `IS NOT NULL` | **8,801 → 5** |
| `IX_RequestTitle_ProjectName` | RequestTitles | `ProjectName` | `IS NOT NULL` | **8,801 → 3** |
| `IX_RequestTitle_CondoName` | RequestTitles | `CondoName` | `IS NOT NULL` | **8,801 → 2** |
| `IX_RequestTitle_RoomNumber` | RequestTitles | `RoomNumber` | `IS NOT NULL` | **8,801 → 2** |
| `IX_RequestTitle_LicensePlateNumber` | RequestTitles | `LicensePlateNumber` | `IS NOT NULL` | **8,801 → 2** |
| `IX_RequestTitle_LandParcelNumber` | RequestTitles | `LandParcelNumber` | `IS NOT NULL` | **8,801 → 335** |
| `IX_Request_RequestorName` | Requests | `RequestorName` | `IsDeleted = 0` | **5,280 → 3** |
| `IX_Request_ContactPersonName` | RequestDetails | `ContactPersonName` | `IS NOT NULL` | **1,386 → 5** |
| `IX_Request_ContactPersonPhone` | RequestDetails | `ContactPersonPhone` | `IS NOT NULL` | (same shape as above) |
| `IX_Request_PrevAppraisalNumber` | RequestDetails | `PrevAppraisalNumber` | `IS NOT NULL` | (same shape as above) |
| `IX_RequestCustomer_ContactNumber` | RequestCustomers | `ContactNumber` INCLUDE `RequestId` | `IS NOT NULL` | **921 → 5** |

Combined effect on the search query's filter stage (six arms unioned, `TOP 200` each,
`OPTION (RECOMPILE, MAXDOP 1)`): **110 ms → 20 ms CPU**.

### Why every un-indexed arm cost exactly 8,801 reads

`request.RequestTitles` is a **67-column TPH table** (`Land`, `LandBuilding`, `Building`, `Condo`,
four `Lease*` variants, `Vehicle`, `Machine`, `Vessel` all in one table) with a **random-GUID
clustered key**. With no index on the searched column, every arm degrades to the same clustered
scan, so cost is independent of how selective the term is — the same shape as finding 4 in this
audit, and the same shape the `vw_AppraisalList` window functions had.

### On adding six indexes to a table that will reach millions of rows

Only **one** of them is dense. TPH means a row populates just its own branch's columns, so the
filtered indexes are proportional to that branch:

- `OwnerName` — set on ~100% of rows. This is the only large index in the batch.
- `ProjectName` — only where the requester typed a project/village name.
- `CondoName`, `RoomNumber` — condo rows only (`TitleFamily` = `U`/`LSU`).
- `LicensePlateNumber` — vehicle rows only (`TitleFamily` = `VEH`). Sparsest of the batch.
- `LandParcelNumber` — land-bearing rows only.

`request.RequestTitles` is written once at intake and rarely updated, so the write cost lands on
insert, not on a hot update path.

### Declaring indexes on a TPH hierarchy

`LandParcelNumber`, `CondoName`, `RoomNumber` and `TitleNumber` are each mapped by **several**
derived-type configurations onto the **same physical column**. Each index is therefore declared
**exactly once**, on one representative configuration — the convention the pre-existing
`IX_TitleDeedInfo_TitleDeedNumber` already follows (declared only on `TitleLandConfiguration`).
Declaring it in every branch would emit duplicate `CREATE INDEX` statements for one column.

### ⚠️ Verifying these: `QUOTED_IDENTIFIER` must be ON

SQL Server **refuses to use a filtered index** when `QUOTED_IDENTIFIER` is `OFF`, and `sqlcmd`
defaults to `OFF`. A benchmark run without `-I` reports the *unchanged* 8,801 reads for every index
above and looks like the work did nothing. Always pass `-I`:

```bash
docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'P@ssw0rd' -C -I -d CollateralAppraisal -i /var/opt/mssql/q.sql
```

The application is unaffected — `Microsoft.Data.SqlClient` sets `QUOTED_IDENTIFIER ON` on connect.

### Not built

- **`appraisal.Appraisals (CreatedAt)`** — considered as the default `ORDER BY` of the appraisal
  list, then dropped: the concurrent `vw_AppraisalList` work measured `Appraisals (Status, CreatedAt)`
  at 84 → 86 ms, i.e. no effect. The cost there was the view's window functions, not the sort.
- **`INCLUDE(RequestId)` on the owned-type indexes** (`ProjectName`, `CondoName`, `RoomNumber`,
  `LandParcelNumber`, `LicensePlateNumber`) — `RequestId` belongs to the base entity, not to the
  owned value object the index is declared on, so EF cannot express it. Not needed in practice: the
  measured seeks are 2–5 reads because the arms are prefix-bounded and `TOP`-capped.

---

## Low-priority / optional (documented, not built)

Marginal because the query shape or column selectivity caps the benefit. Add only if profiling
shows real cost:

- **`AppraisalProperties.PropertyType`** — History Search collateral-type `EXISTS`. The geo/spatial
  prefilter already narrows rows first. Candidate: `(AppraisalId, PropertyType)`.
- **Land/Condo `AppraisalDetails` address columns (Province/District/SubDistrict)** — real columns
  (`HasColumnName`, not JSON). `vw_AppraisalList` picks the location with a correlated
  `OUTER APPLY … TOP 1` and filters after the pick, so an index on Province still cannot drive the
  *filter*. What it can help is the *sort*: a covering
  `(AppraisalPropertyId) INCLUDE (Province, District, SubDistrict)` was measured on ~105k appraisals
  (2026-08-30) at 1,280 ms → 675 ms CPU for `sortBy=province`, but only 111 ms → 58 ms elapsed.
  Not built — revisit if province sorting becomes a hot path.
- **`PendingTasks.TaskStatus`** — monitoring view hard filter. The active queue is bounded (completed
  rows move to `CompletedTasks`), so a scan stays cheap. Revisit only if the active queue grows large.

---

## Structural concerns (no index can fix)

1. **Leading-wildcard search** (`LIKE '%term%'` on AppraisalNumber / CustomerName / RequestNumber) in
   every list view is non-sargable. Options if search latency grows: restrict to prefix (`'term%'`)
   search, or add a SQL Server full-text index on the searched columns. Acceptable at current scale.
2. **`vw_TaskList` = UNION ALL of 4 branches**, each scanning PendingTasks and resolving a different
   correlation root, then heavy outer enrichment. The list endpoint materializes the whole view then
   paginates, and `QueryPaginatedAsync` also runs a **COUNT over the full view** per page. Mitigated
   today: the active PendingTasks set is bounded and `/tasks/counts` already bypasses the view (reads
   PendingTasks directly). This is the main thing to watch as volume grows.
3. **Some monitoring handlers return the full result set (no pagination)** — paginate if breach lists
   grow large.
4. **`TRY_CAST(AssigneeCompanyId AS uniqueidentifier)` → auth.Companies** join: the cast is on the
   outer column, so the `Companies.Id` PK seek is unaffected — OK, no action.

### 7. `vw_AppraisalList` query shape (found 2026-08-30, fixed)

This audit rated the Appraisal List "mostly ✅" because it only looked at index coverage. It was not
an index problem. The view picked the latest assignment and the first land location with
`LEFT JOIN (SELECT …, ROW_NUMBER() OVER (PARTITION BY …) rn …) x ON … AND x.rn = 1`. With `rn = 1`
outside the derived table the optimizer must number **every** row of the underlying table before the
outer `WHERE` can apply, so no index and no filter selectivity could help: returning a single-row
result still cost ~580 ms of CPU, and one 20-row page under `status=Pending` cost ~11 s of CPU
(3.7M Worktable logical reads, a 14M-row Table Spool, and 105,348 executions of the customer APPLY).

Compounding it, the handler ran the view **three times** per request — a `COUNT(*)` wrapper, the page,
and an unpaginated facet pass that pulled every matching row into memory to `GroupBy` in C#.

Measured on ~105k appraisals, k6 against `GET /appraisals`: p95 23.4 s at 8 concurrent users,
throughput 1.27 req/s. After rewriting the two windows as `OUTER APPLY … TOP 1`, paging Ids before
enriching, counting off the base table where the filter allows, and grouping the facet in SQL:
p95 1.28 s, throughput 16.5 req/s.

**Lesson for future audits: check the shape before checking the indexes.** An index is unusable if
the query cannot let the optimizer reach it, and a coverage matrix cannot see that.

---

## Alternatives considered

- **Finding #2 via view rewrite instead of an index:** realign the two `vw_TaskList` joins to
  `u.NormalizedUserName = UPPER(ISNULL(a.RequestedBy, r.Requestor))`, reusing the existing unique
  Identity index (exactly what `vw_MonitoringPendingTasks` already does). This needs no new index but
  is a view change (out of the chosen scope: "audit + index migrations"). If adopted, drop
  `IX_AspNetUsers_UserName`. Recommended as a follow-up if the team prefers fewer indexes.
- **Single DbUp SQL script** for both indexes (`IF NOT EXISTS (sys.indexes…) CREATE INDEX …`) instead
  of two EF migrations. Rejected to keep the EF model authoritative and avoid drift; both target tables
  are EF-managed entities.

---

## Verification

1. **Scaffold check (done):** `dotnet build` succeeds; each migration's `Up()` contains exactly one
   `CreateIndex` with the intended INCLUDE and nothing else (no model drift). `database update` **not**
   run by this change.
2. **Plan proof (run against a populated DB):**
   - Quotation list as a non-admin user → before: Clustered Index Scan on `CompletedTasks`; after:
     Index Seek on `IX_CompletedTasks_CorrelationId`.
   - `SELECT TOP 50 * FROM workflow.vw_TaskList ORDER BY AssignedDate DESC` → the two `AspNetUsers`
     joins switch from Scan to Seek on `IX_AspNetUsers_UserName`.
3. **Regression smoke:** load Task List (me + pool), Appraisal List, Monitoring tabs, History Search
   (geo + non-geo), and Quotation List (admin + external); confirm results unchanged and latency not
   worse.
