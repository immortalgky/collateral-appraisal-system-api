# GET /appraisals — performance investigation and fix

**Date:** 2026-08-30 · **Branch:** `feat/your-profile-page` · **Dataset:** 105,475 appraisals (local)

## The report

"หน้าเสิช appraisals/list เริ่มช้าเวลาเสิชด้วยเงื่อนไขต่างๆ เช่น appraisalStatus" — with an explicit
instruction to prove it with measurements and execution plans rather than guess.

## Confirmed, and worse than it felt

One page load ran **three** statements sequentially on one non-MARS connection:

| # | Statement | CPU | Elapsed |
|---|---|---|---|
| 1 | `SELECT COUNT(*) FROM (SELECT * FROM vw_AppraisalList <where>) AS CountQuery` | 591 ms | 86 ms |
| 2 | `SELECT * FROM vw_AppraisalList <where> ORDER BY CreatedAt DESC OFFSET 0 FETCH 20` | **11,255 ms** | 985 ms |
| 3 | facets — every matching row (105,348) pulled into C# and grouped in memory | 807 ms | 174 ms |

≈ **12.6 CPU-seconds per request** to return 20 rows, on 14 vCPU ⇒ a system ceiling near 1.3 req/s.

### Why statement 2 cost 11 CPU-seconds

`vw_AppraisalList` resolved the latest assignment and the first land location with

```sql
LEFT JOIN (SELECT …, ROW_NUMBER() OVER (PARTITION BY …) AS rn FROM <whole table>) x
       ON x.AppraisalId = a.Id AND x.rn = 1
```

`rn = 1` sits **outside** the derived table, so the optimizer has to number every row of the
underlying table before the outer `WHERE` can apply — no index and no filter selectivity can reach
past it. `SET STATISTICS IO` / `PROFILE`:

```
Table 'Worktable'.        Scan count 14,      logical reads 3,753,410
Table 'RequestCustomers'. Scan count 105,348, logical reads   337,079
14,268,890 rows  x144      --Table Spool
 1,476,594 rows  x14       --Sequence Project(DEFINE:([Expr1023]=row_number))
```

Consequence: cost was nearly independent of the filter. Narrowing to a **single row** still burned
~580 ms of CPU. That is why "everything got slower at once" as data grew.

### Load test (k6, `docs/load-test/search-appraisals.js`)

| | Before | After |
|---|---|---|
| 1 VU — avg / p95 | 1.09 s / 1.69 s | **113 ms / 215 ms** |
| 8 VU — avg / p95 / max | 6.17 s / **23.36 s** / 31.21 s | **415 ms / 1.03 s / 2.29 s** |
| 8 VU — throughput | 1.27 req/s | **17.4 req/s** |
| 20 VU — p95 / throughput | (not run; 8 VU already 23 s) | 2.59 s / 20.4 req/s |

The frontend sets a **10 s** axios timeout with React Query's default 3 retries and no
`AbortController`, so a p95 of 23 s did not just fail — it turned one user's search into four
requests, each burning 11 CPU-seconds. That feedback loop is now out of reach.

## Changes

1. **`Database/Scripts/Views/Appraisal/vw_AppraisalList.sql`** — both `ROW_NUMBER()` derived tables
   rewritten as correlated `OUTER APPLY … TOP 1`. Column names, order, types and values unchanged.
   `la` deliberately stays ahead of `comp` and `apt`, which both reference it.
2. **`GetAppraisalsQueryHandler`** — pages **Ids** through the view first, then enriches only that
   page (`WHERE Id IN @Ids`, repeating the ORDER BY because `IN` does not preserve order). Selecting
   just `Id` lets the optimizer drop the APPLYs the filter and sort never read.
3. **Count** — uses the existing `countSql` overload of `QueryPaginatedAsync` to count off
   `appraisal.Appraisals` whenever no predicate needs a view-only column. `COUNT(*)`, never
   `COUNT_BIG`, because the helper reads the scalar as `int`.
4. **Facets** — a `GROUP BY` in SQL returning ~6 rows instead of 105k rows grouped in C#, and
   **only `status`**: the other four groups are in the contract but no client reads them, and
   `assignmentType` alone cost more than everything else combined. They return empty arrays, so the
   response shape is unchanged.
5. **Status-chip bug (fixed as requested)** — the facet WHERE now excludes the status predicate.
   Before, picking "Completed" left exactly one chip, so there was nothing to click back to.
6. **`AppraisalFilterBuilder`** — returns `AppraisalFilterSql` carrying `RequiresView`, the gate for
   3 and 4, plus an `excludeStatus` option for 5. A two-value `Deconstruct` keeps the existing Export
   and Quotation callers compiling unchanged.
7. **`Database/Migration/Scripts/20260827090000_Cleanup_RetireRegulatoryV1V2.sql`** — *out of scope,
   but it blocked all verification.* It does `DELETE FROM hangfire.[Hash]`; Hangfire builds its
   schema at application start, not through migrations, so on any database that has never run the
   app (every integration-test container) the whole DbUp run aborted with
   `Invalid object name 'hangfire.Hash'`. **The entire integration suite was red because of this** —
   `AppraisalChainResolutionTests` failed 7/7 on untouched code. Now guarded with `OBJECT_ID(...)`.

## Verification

- **Equivalence.** The candidate view was built under a temporary name and compared against the live
  one: column shape (name, ordinal, type, length, precision, scale, nullability) — 0 differences;
  row count 105,475 = 105,475; `EXCEPT` in both directions across all 40 columns — 0 and 0.
  Column shape matters beyond this view: RCAS001/002/004/008/009/010 bind `SELECT *` to **positional**
  records, so a shifted column would corrupt reports silently.
- **API.** `GET /appraisals?status=Pending&pageSize=20` returns byte-identical `items` and the same
  `count` as before the change; `facets.status` went from 1 chip to 6 (the intended fix).
- **IO after.** Worktable 3,753,410 → **127** logical reads; RequestCustomers 105,348 scans → **20**;
  LandAppraisalDetails 87,052 → **99**.
- **Tests.** 40 new unit tests (`AppraisalFilterBuilderTests`) pin the emitted WHERE per filter field
  and, critically, which side of `RequiresView` each field is on. 6 new integration tests
  (`AppraisalListViewRowSelectionTests`) create the multi-candidate cases the EXCEPT check cannot
  reach. Full `Appraisal.Integration.Tests` suite: **27/27 pass** (was 0/27).
- **Downstream.** All 9 dependent SQL views queried successfully; `/appraisals`,
  `/appraisals/export`, `/appraisals/eligible-for-quotation` all 200.

## Measured and rejected

Kept out on evidence, not opinion:

- **Hybrid view shape** (`la` as APPLY, `ld` left as a window): **52,534 ms CPU** — five times worse
  than the original. Do not try this.
- **Covering indexes.** Four candidates built and benchmarked. Three moved nothing:
  `AppraisalAssignments` covering (1,229 → 1,241 ms), `Appointments (AssignmentId, AppointmentDateTime)`
  (3,030 → 2,527 ms, inside noise), `Appraisals (Status, CreatedAt)` (84 → 86 ms). Only
  `LandAppraisalDetails (AppraisalPropertyId) INCLUDE (Province, District, SubDistrict)` helped —
  1,280 → 675 ms CPU for `sortBy=province`, but just 111 → 58 ms elapsed. Not shipped: the column
  lives in an owned value object, so EF cannot express the INCLUDE without a hand-authored migration,
  and 53 ms on one sort option does not justify that. Documented in the audit doc for later.

## Code review — findings and what changed

Reviewed after the work was complete; four findings, all acted on.

**1. Regression I introduced (medium) — an unbounded `pageSize` became a 500.**
The enrichment step binds one parameter per id (`WHERE Id IN @Ids`) and SQL Server caps a statement
at 2100 parameters, while `PaginationRequest` has no upper bound and is bound straight off the query
string. Reproduced end-to-end: `pageSize=2000` → 200 OK, `pageSize=2099` → 500
(*"The incoming request has too many parameters. The server supports a maximum of 2100"*). The old
single-query shape had no such limit, so this was mine.
Fixed by clamping to `MaxPageSize = 200` — the ceiling `GetAuditLogs` and `GetUserAccessMatrix`
already use. Clamped rather than rejected so an oversized request still returns data; callers read
the effective size back off `PaginatedResult.PageSize`. Verified: 2099 / 5000 / 100000 all return
200 rows with `pageSize: 200` and an exact `count`, so paging stays consistent.
It also closes a pre-existing hole — `?pageSize=100000` used to enrich 100k rows through the view.

**2. Neither hand-rolled Dapper call passed the cancellation token.** The handler threaded it into
`IBusinessTimeCalculator` but not into the enrichment or facet queries. On the external-company path
both always read the view, so a user navigating away left two full scans running. Both now use
`CommandDefinition(..., cancellationToken: ...)`.

**3. The facet contract advertised five groups and shipped four permanently empty.** A consumer
could not tell "not computed" from "no matching rows". The four are kept (removing them is a
breaking change nobody asked for) but are now documented as always-empty on `AppraisalFacets`, with
a pointer to the opt-in `?groupBy=` shape `/tasks/me/group-counts` uses if one is ever needed.

**4. `RequiresView` is a hand-maintained flag set at ~10 call sites.** Correct for every field today
(the reviewer checked each against the EF model, and `AppraisalFilterBuilderTests` pins them), but a
future view-only filter that forgets to set it emits a WHERE the base table cannot bind — and it
fails only on the count/facet statements, so the list would still render while the total and chips
500. Not changed here; deriving the flag from a column→source map is the fix, tracked below.

Also removed during review: `AppraisalFilterSql` originally carried a two-value `Deconstruct` so the
Export and Quotation callers compiled unchanged. That hid `RequiresView` at exactly the call sites
most likely to misuse it later, which is not worth saving two lines — both now destructure
`(whereClause, parameters, _)` with a comment saying why the flag is discarded.

## Notes / follow-ups

- `GET /appraisals?sortBy=appointmentDateTime` is the slowest remaining shape (~2.8 CPU-seconds for
  the key query; pinned at 50 VUs it runs p95 12.4 s / 6.2 req/s against 0.88 s / 63.8 req/s for
  `status=Pending`). It was already second-slowest before and did not regress. Worth noting it is
  **not** what caps the page: removing it from the weighted mix at 50 VUs moves p95 only
  5.77 s → 5.09 s. Fix it if users sort by appointment date often, not to raise capacity.
- **Capacity.** `docs/load-test/search-appraisals.js` draws query shapes from a weighted mix (the
  weights are an assumption from the UI, overridable with `-e WEIGHTS=…`; replace them with real
  query-string logs when available). On this 14-vCPU box the endpoint tops out at **~22 req/s**;
  past that, latency is pure queueing — 50 VUs ÷ 22 req/s ≈ 2.3 s, which is what the run measures
  (avg 2.07 s). Documented real load is ~0.28 req/s, so roughly 80x headroom.
- The FE's 10 s axios timeout with 3 silent retries and no `AbortController` is a load amplifier
  that is independent of this fix. Worth its own ticket.
- **Derive `RequiresView` from a column→source map** instead of ~10 hand-set flags (review finding 4).
  A single table of view-only column names would make it impossible to add a predicate and silently
  skip the gate.
- SonarCloud runs Oracle plsql rules against T-SQL here; the view body was rewritten wholesale, so
  pre-existing findings may resurface as "new code".
- `docs/load-test/get-appraisal-token.sh` exists because `X-Dev-Auth: dev-bypass` stamps
  `company_id = Guid.Empty`, which `AppraisalAccessScope` turns into a filter matching nothing —
  dev-bypass returns 0 rows and cannot be used to measure this endpoint at all.
