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
| latest active assignment APPLY | `AppraisalId, AssignedAt DESC` (filtered) | AppraisalAssignments | `IX_AppraisalAssignments_AppraisalId_AssignedAt_Active` | ✅ |
| company name join | `comp.Id = TRY_CAST(...)` | auth.Companies | PK (seek on right side) | ✅ |
| latest appointment APPLY | `AssignmentId, Status` | Appointments | `IX_Appointments_AssignmentId`, `_Status` | ✅ |
| value join | `va.AppraisalId` | ValuationAnalyses | `IX_ValuationAnalyses_AppraisalId` (unique) | ✅ |
| customer name | `c.Name`, `RequestId` | RequestCustomers | `IX_RequestCustomer_Name`, `_RequestId` | ✅ |
| filter Province / District / SubDistrict | `ld.Province` … | LandAppraisalDetails | none | ⚠️ see §Low-priority — view computes location via `ROW_NUMBER()` then filters *after* the window, so an index here is **not usable** |
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

## Low-priority / optional (documented, not built)

Marginal because the query shape or column selectivity caps the benefit. Add only if profiling
shows real cost:

- **`AppraisalProperties.PropertyType`** — History Search collateral-type `EXISTS`. The geo/spatial
  prefilter already narrows rows first. Candidate: `(AppraisalId, PropertyType)`.
- **Land/Condo `AppraisalDetails` address columns (Province/District/SubDistrict)** — real columns
  (`HasColumnName`, not JSON). In `vw_AppraisalList` location is derived via a `ROW_NUMBER()` window
  and filtered *after* the window, so an index is **not usable** there; only the History Search
  `EXISTS` could use one, and these are low-selectivity text columns.
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
