# Load test: ~100k applications end-to-end

Drives the **real** create+submit pipeline at scale so the whole stack is exercised. The
Integration endpoint `POST /api/v1/requests` **creates AND submits in one call**
(`CreateAndSubmitRequestAsync`, entrySource `"API"`) — no separate submit call:

```
k6 HTTP  ──►  POST /api/v1/requests   (creates + submits; EF write)
                     │  raises RequestSubmittedEvent
                     ▼
              IntegrationEventOutbox  ──►  RabbitMQ
                     ▼                        │
              Workflow consumer  ─────────────┘  (creates WorkflowInstance)
                     ▼
              Appraisal consumer  (AppraisalCreationService builds the Appraisal)
```

The **HTTP call returns once the event is in the outbox** — it does *not* wait for the
appraisal. So there are **two** results to measure:

1. **Synchronous HTTP** throughput/latency of the create+submit call → from the k6 summary.
2. **Async drain** → how fast the bus + consumers turn N submitted requests into N
   appraisals → from `monitor.sql` + the RabbitMQ UI. This continues *after* k6 finishes.

## Files

| File | Purpose |
|---|---|
| `search-tasks.js` | **read-side** — k6 script: load-test `GET /tasks/pool` search ("SearchTask"); **bearer token** auth |
| `create-submit.js` | **phase 1** — k6 script: create+submit (dev-bypass auth, randomized payloads) |
| `pools.js` | valid-code pools (synchronized geo triples, names) — shared by both phases |
| `monitor.sql` | drain + correctness + TPS queries (run during/after phase 1) |
| `export-worklist.sql` | **phase 2** — export `(appraisalId, propertyId, propertyType)` of load-test properties (L/LB/U) to CSV |
| `fill-detail.js` | **phase 2** — k6 script: fill property detail per family (land / land+building / condo) for each work-list row |
| `export-groups.sql` | **phase 3** — export `groupId` of each load-test appraisal's PropertyGroup to CSV |
| `fill-pricing.js` | **phase 3** — k6 script: give each group a concluded value via Pricing Analysis (create → start → complete) |
| `cleanup.sql` | reset the load-test data (scoped to the `loadtest` marker) |

## Prerequisites

- **k6** installed (`brew install k6`).
- App running, ideally **Release** for representative numbers:
  `dotnet run -c Release --project Bootstrapper/Api`
- Full infra up (SQL + **RabbitMQ** + Redis + Seq): `docker compose up -d`
- Auth: the script sends header `X-Dev-Auth: dev-bypass` — no OAuth token needed in dev.

## What the payload looks like

Built from valid pools so every record passes validation but the data distribution is
realistic (no single-value indexes). **Fixed** per decision: `purpose:"01"`, `channel:"MANUAL"`,
`uploadSessionId:null`, `documents:[]`. **Varied**: `titles[].collateralType` randomly picked
from `01/02/08` with `properties[].propertyType` set to the matching appraisal family
(`01→L` Land, `02→LB` Land+Building, `08→U` Condo) — the family is derived from the title code
by `AppraisalCreationService.CodeToAppraisalFamily`; plus a synchronized
province/district/subdistrict triple (derived by prefix from a real subdistrict code),
customer/owner names + phones, amounts, appointment date, unique title numbers.
Marker: `requestor.userId = creator.userId = "loadtest"`.

## Run it

**1) Smoke (n=1) — do this first.** Proves auth + purpose `01` + collateralType `01`
(empty docs) + the bus all work before scaling. If the call returns **400**, the most likely
cause is the `IRequestDocumentValidator` (it runs on this create+submit endpoint) requiring
documents for purpose `01`; pick a purpose whose required-document set is empty (or attach the
required `documentType`s) and retry.

```bash
k6 run -e BASE_URL=https://localhost:7111 -e TARGET=1 -e VUS=1 \
       --insecure-skip-tls-verify docs/load-test/create-submit.js
```

Then in `monitor.sql` confirm a WorkflowInstance and an Appraisal appear within a few
seconds, and that the title produced a property on the appraisal (collateralType `"01"` is a
numeric code → Land family, so a property is created).

**2) Small load (n=1000).** Watch RabbitMQ (http://localhost:15672, admin/P@ssw0rd) queue
depth rise then drain; confirm `appraisals_total` reaches +1000.

```bash
k6 run -e BASE_URL=https://localhost:7111 -e TARGET=1000 -e VUS=25 \
       --insecure-skip-tls-verify docs/load-test/create-submit.js
```

**3) Full (n=100000) — count mode.** Exact data volume + async-drain test.

```bash
k6 run -e BASE_URL=https://localhost:7111 -e TARGET=100000 -e VUS=50 \
       --insecure-skip-tls-verify docs/load-test/create-submit.js
```

**4) Capacity / stress — rate mode.** Production-grade ramp toward `PEAK_RPS` to find the
knee (see *Choosing the load level* below for how to pick `PEAK_RPS`).

```bash
k6 run -e MODE=rate -e PEAK_RPS=100 -e BASE_URL=https://localhost:7111 \
       --insecure-skip-tls-verify docs/load-test/create-submit.js
```

### Tunables (env vars)

The script has **two modes**, selected by `MODE` (only one scenario runs at a time):

| Env | Default | Applies to | Meaning |
|---|---|---|---|
| `MODE` | `count` | both | `count` = exact total; `rate` = RPS ramp |
| `BASE_URL` | `https://localhost:7111` | both | API base |
| `TARGET` | `100000` | count | exact number of applications |
| `VUS` | `50` | count | concurrent virtual users |
| `PEAK_RPS` | `100` | rate | expected peak requests/sec (ramp scales off this) |
| `PRE_VUS` | `50` | rate | VUs pre-allocated for the ramp |
| `MAX_VUS` | `500` | rate | upper bound k6 may allocate |
| `WARMUP` | `2m` | rate | warm-up stage duration |
| `STAGE_DUR` | `5m` | rate | duration of each load stage |

- **count mode** (`shared-iterations`): exact `TARGET` applications at concurrency `VUS`.
  Use for data-volume seeding and the async-drain test.
- **rate mode** (`ramping-arrival-rate`): holds a req/s target and auto-allocates VUs. The
  ramp is `0.5× → 1× → 2× → 4× PEAK_RPS` (each `STAGE_DUR`) then down — i.e. it walks past
  your expected peak to **find the knee**. Use for the production-grade capacity test.

## Choosing the load level (production-grade)

**Don't pick a VU number directly — pick a target throughput, then VUs fall out of it.**
A VU count alone is meaningless because throughput = VUs ÷ latency. "50 VUs" is wildly
different load depending on how fast each call is.

### VUs are concurrency, not load (Little's Law)

With the `shared-iterations` executor, `VUS` = simultaneous in-flight requests. The actual
request rate is:

```
RPS  ≈  VUs / avg_response_time          (throughput from concurrency)
VUs  ≈  target_RPS × avg_response_time   (concurrency needed for a target rate)
```

| avg create+submit latency | VUs to reach 100 req/s | VUs to reach 200 req/s |
|---|---|---|
| 50 ms  | 100 × 0.050 = **5**  | **10** |
| 100 ms | 100 × 0.100 = **10** | **20** |
| 250 ms | 100 × 0.250 = **25** | **50** |

So measure single-request latency first (the baseline run below), then compute VUs for the
rate you actually want. **For a production-grade test, drive a target *rate* with a
`ramping-arrival-rate` executor** (it auto-allocates VUs to hold the RPS) rather than guessing
a fixed VU count — see the snippet at the end.

### Derive the target rate from the real workload

For an internal bank appraisal system the workload is usually **applications/hour at peak**,
not high RPS. Convert it:

```
peak_RPS = applications_in_peak_hour / 3600
```

Worked example: 5,000 applications/day, ~60% landing in a 3-hour busy window
→ 3,000 / 3 = 1,000 applications/hour → **~0.28 req/s** baseline. Even at **10× headroom**
that is only ~3 req/s — i.e. real steady load here is *low*; the value of the test is the
**stress probe** that finds the breaking point, not sustaining a big number.

If you don't know the peak, run a **step/stress ramp** and report the **knee**: the highest
rate that still holds acceptable p95 **and** keeps the async drain flat.

### Two caveats that matter more than the VU number

1. **The environment must match prod or the number is fiction.** Prod is **2 IIS app servers
   + 1 SQL + 1 Redis behind F5** (N=2 replicas). A laptop-vs-`localhost` run shows relative
   trends, not production capacity. For a real grade: run against a prod-like 2-node
   deployment, **in Release**, and **run k6 from a separate machine** (k6 and the app fighting
   for the same CPU skews every number).
2. **The bottleneck is the async drain, not the HTTP call.** Create+submit only writes a
   request row + an outbox row — cheap, so HTTP RPS looks great even at high VUs. The real
   limit is whether the **Workflow + Appraisal consumers + SQL** can drain the outbox/RabbitMQ
   at your arrival rate *sustainably*. Production-grade pass/fail is therefore: **at the
   sustained target rate, does the outbox backlog stay flat (consumers keep up) or grow
   unbounded (consumers fall behind)?** Watch the `monitor.sql` backlog + RabbitMQ queue depth
   during the steady-state stage — that is the real capacity signal.

### Recommended procedure

1. **Baseline** — `VUS=1`, ~500 iterations. Record single-request p50/p95; this is your
   latency constant for the table above.
2. **Capacity / stress** — ramp the *rate* (snippet below) until one of these breaks:
   p95 exceeds your SLA, `http_req_failed` > ~1%, **or the outbox backlog grows and doesn't
   recover**. Take that rate, shave ~30% for headroom → your stated capacity.
3. **Fixed-VU alternative** — if you must use `shared-iterations`, start `VUS=50`, then step
   `100 → 200 → 400`, watching the same three signals. Arrival-rate is the better tool, though.

As a starting **stress ceiling** for a 2-node setup, ramp toward **200–500 req/s** — you will
almost certainly find the consumer/SQL knee well before the HTTP layer strains. That is a
probe to *find* the limit, not a recommended steady load.

### Arrival-rate mode (built in)

Run the rate ramp with `MODE=rate` and your expected peak:

```bash
k6 run -e MODE=rate -e PEAK_RPS=100 -e BASE_URL=https://localhost:7111 \
       --insecure-skip-tls-verify docs/load-test/create-submit.js
```

It ramps `0.5× → 1× → 2× → 4× PEAK_RPS` (each `STAGE_DUR`, default 5m) then back to 0 — so it
deliberately overshoots your expected peak to find the knee. Tune with `PEAK_RPS`, `STAGE_DUR`,
`WARMUP`, `PRE_VUS`, `MAX_VUS` (see the env table above). k6 reports `dropped_iterations` when
it can't hit the target rate — itself a signal that the system (or `MAX_VUS`) is the limit.

## Reading the results

- **HTTP**: k6 summary — `http_req_duration{name:create_request}` p95, `http_reqs` rate,
  `http_req_failed` rate, `applications_submitted` counter, `checks` pass rate.
- **Async drain**: run `monitor.sql` repeatedly. The interesting number is the **time for
  `appraisals_total` to reach TARGET after the k6 run ends**, plus the outbox backlog
  (`IntegrationEventOutbox` Pending/Processing) and the RabbitMQ queue depth.

## Measuring TPS (transactions/sec)

**TPS = appraisals actually created per second** — the real business throughput, and the number
the *rate-mode* knee is about. It is **not** in the k6 output (k6 only sees the HTTP call, which
returns at the outbox); it is measured **DB-side**, because the Appraisal consumer finishes the
transaction. The clock is `appraisal.Appraisals.CreatedAt`, stamped by the consumer at creation.

`monitor.sql` queries 7–10 do this — set `@runStart` to your k6 start time first:

1. **Per-minute time series (query 7)** — the **flat top (plateau)** is your sustained TPS
   (`per_min / 60`). Switch the bucket to second for high-rate runs.
2. **Average over the window (query 8)** — total appraisals ÷ elapsed seconds.
3. **Two-sample live (query 9)** — run it, wait ~30 s, run again → `(count2 − count1) / 30`.
4. **Stage TPS (query 10)** — workflow instances/sec, to see whether the **Workflow** consumer or
   the **Appraisal** consumer is the limiting stage.

**Validity rule:** a TPS figure is *sustainable capacity* only if the outbox backlog (query 4) and
the RabbitMQ queue depth stay **flat** during the window. If they **grow**, you've measured the
**max drain rate** (the knee) while offered RPS exceeds it — which is exactly what the rate-mode
ramp is trying to surface.

**Live proxies (no SQL):**
- **RabbitMQ UI** (`http://localhost:15672`, admin/P@ssw0rd) → the consumer queue → the **ack /
  deliver rate** (messages/sec) ≈ TPS at that stage.
- **Seq** (`http://localhost:5341`) → count `AppraisalCreationService` "Successfully created
  appraisal …" log lines per second.

## If the async drain is the bottleneck (tuning, optional)
- **MassTransit consumer concurrency / prefetch** for the Workflow + Appraisal consumers —
  the main lever for appraisal-creation throughput.
- **Outbox delivery interval / batch size** (the `IntegrationEventDeliveryService` poller) —
  adds baseline latency to every event.
- The `AppraisalCreationService` runs a 3-phase transaction with several `SaveChanges`,
  an SLA lookup, and appendix generation per appraisal — expect this to be the per-record
  cost center.

## Read-side: load-test the task search ("SearchTask")

`search-tasks.js` exercises `GET /tasks/pool` with a `search` filter — the transaction the
load-test report flagged at **7.6–9.6s**. The slow path is the leading-wildcard
`LIKE '%term%'` over the heavy `workflow.vw_TaskList` view, run **twice** per request (count +
page) whenever a search/customer-name filter is active. `/tasks/pool` is slow because it scans
the whole group/team pool; `/tasks/me` pre-filters to one user and stays fast (point
`-e ENDPOINT=/tasks/me` to confirm).

**Auth is different from the other phases:** the pool query scopes rows by the caller's
groups/team/company, so dev-bypass won't exercise the real candidate set. You must pass a
**real access token** via `TOKEN` (the `Bearer ` prefix is added for you if missing). Grab a
token from an authenticated session (browser dev-tools → a `/tasks/pool` request's
`Authorization` header, or the `/connect/token` response).

```bash
export TOKEN='eyJhbGci...'      # access token of a user who HAS pool tasks

# 1) Baseline — single-request latency (run first):
k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
       -e MODE=count -e VUS=1 -e ITERATIONS=100 \
       --insecure-skip-tls-verify docs/load-test/search-tasks.js

# 2) Concurrency — reproduce the report's contention:
k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
       -e MODE=count -e VUS=20 -e ITERATIONS=500 \
       --insecure-skip-tls-verify docs/load-test/search-tasks.js

# 3) Capacity / stress — ramp the rate to find the knee:
k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
       -e MODE=rate -e PEAK_RPS=20 \
       --insecure-skip-tls-verify docs/load-test/search-tasks.js

# 4) Typing simulation — one request per keystroke (P, Pe, Per, ...), like the FE:
k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
       -e MODE=count -e VUS=10 -e ITERATIONS=200 -e TYPE=true -e SEARCH=Performance \
       --insecure-skip-tls-verify docs/load-test/search-tasks.js
```

### Tunables (env vars)

| Env | Default | Meaning |
|---|---|---|
| `TOKEN` | *(required)* | Bearer access token; the user's groups/team scope the pool rows |
| `BASE_URL` | `https://localhost:7111` | API base |
| `ENDPOINT` | `/tasks/pool` | endpoint under test; set `/tasks/me` to compare |
| `MODE` | `count` | `count` = exact iterations; `rate` = RPS ramp |
| `VUS` / `ITERATIONS` | `10` / `300` | count-mode concurrency / total requests |
| `PEAK_RPS` | `20` | rate-mode target req/s (ramps `0.5×→1×→2×→4×`) |
| `SEARCH` | `a` | the search term (matches AppraisalNumber OR CustomerName) |
| `SEARCH_FIELD` | `search` | `search` (top box) or `customerName` (explicit column filter) |
| `TYPE` | `false` | simulate typing: one request per growing prefix of `SEARCH` |
| `THINK_MS` | `150` | delay between keystrokes when `TYPE=true` |
| `VARY` | `false` | append a random suffix per request so terms differ |
| `PAGE_SIZE` / `SORT_BY` / `SORT_DIR` | `25` / *(server default)* / `desc` | query shape |

The threshold `http_req_duration{name:search_task} p(95)<2000` encodes the goal — a clean run
should be under 2s once the A+B fix lands; today it will breach. Use the baseline run's p50/p95
as the latency constant, then drive `MODE=rate` to find the rate at which p95 blows past your SLA.

## Phase 2: fill appraisal property detail (data at volume)

Phase 1 leaves each appraisal as a **skeleton** — the `AppraisalProperties` rows exist but the
detail columns are mostly empty. Phase 2 fills them so the 100k-record dataset is realistic
for view / report / pagination testing. It is **detail-only** (no pricing, no workflow advancement)
and optimizes for **data completeness**, not throughput.

It targets the **un-gated** update endpoints (no assignment check — any caller with the appraisal +
property id and the dev-bypass header can write), branching on the property family:

```
L  -> PUT /appraisals/{appraisalId}/properties/{propertyId}/land-detail               -> 204
LB -> PUT /appraisals/{appraisalId}/properties/{propertyId}/land-and-building-detail   -> 204
U  -> PUT /appraisals/{appraisalId}/properties/{propertyId}/condo-detail               -> 204
```

These match the families create-submit.js generates (collateralType `01→L`, `02→LB`, `08→U`).
`fill-detail.js` reads the `propertyType` column from the work-list and picks the endpoint + body.

**Run it (after the phase-1 drain has completed):**

1. **Export the work-list** — one `(appraisalId, propertyId, propertyType)` per property under the marker.
   On macOS (no local `sqlcmd`) run it inside the SQL Server container — no install needed:
   ```bash
   docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd \
     -S localhost -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal \
     -s"," -W -h -1 < docs/load-test/export-worklist.sql > docs/load-test/worklist.csv
   ```
   (If you have `sqlcmd` locally: `sqlcmd -S localhost,1433 -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal
   -i docs/load-test/export-worklist.sql -s"," -W -h -1 -o docs/load-test/worklist.csv`.)
   Each line is `appraisalId,propertyId,propertyType` (no header); `SET NOCOUNT ON` keeps the CSV clean.

2. **Smoke first** — trim `worklist.csv` to a single row (ideally one of each family), then:
   ```bash
   k6 run -e BASE_URL=https://localhost:7111 -e VUS=1 \
          --insecure-skip-tls-verify docs/load-test/fill-detail.js
   ```
   Expect the matching `…-detail 204` check to pass. Verify in SQL that the row's detail columns are
   now populated (e.g. `SELECT OwnerName, Latitude FROM appraisal.AppraisalProperties WHERE Id='<propertyId>'`).
   If a coded enum value `"01"` isn't valid for some table the PUT may 400 — the `console.error` line
   prints the server message so you can see which field.

3. **Full run** — restore the full `worklist.csv` and:
   ```bash
   k6 run -e BASE_URL=https://localhost:7111 -e VUS=50 \
          --insecure-skip-tls-verify docs/load-test/fill-detail.js
   ```
   `iterations` equals the work-list length, so every property is filled exactly once
   (the row index comes from `exec.scenario.iterationInTest`). Override the work-list path with
   `-e WORKLIST=./other.csv` if needed.

**Verify completeness:** the `details_filled` counter should equal the work-list length, and
`http_req_failed` ~0. Spot-check a few `AppraisalProperties` rows for non-null detail columns.

> The bodies fill descriptive + numeric fields per family — land (owner, lat/lon, address,
> boundaries, road access, a title with area + government price), building (type, floors, area,
> year, materials), condo (unit/room/floor, usable area, deed). All coded enum (`List<string>`)
> fields default to `["01"]` (single assumed-valid code). Skipped: depreciation/surfaces/
> construction-inspection (LB) and condo area-details — optional, left as no-ops. Swap in
> per-field valid-code pools if a view needs specific labels.

## Phase 3: give each appraisal a concluded value (Pricing Analysis)

Phases 1–2 leave appraisals with no value. Phase 3 sets a realistic appraised value so
value-bearing views/reports (e.g. `vw_AppraisalList.AppraisalValue`) aren't empty. Pricing
Analysis is **per PropertyGroup** (phase 1 auto-creates one "Group 1" per appraisal), and the
minimal path to a persisted value is **3 un-gated calls per group** — no approaches/methods/
comparables required:

```
1. POST /property-groups/{groupId}/pricing-analysis      -> 200 { id, status:"Draft" }
2. POST /pricing-analysis/{id}/start                      -> 200 { status:"InProgress" }
3. POST /pricing-analysis/{id}/complete  { marketValue, appraisedValue, forcedSaleValue }  -> 200
```

`complete` sets `FinalAppraisedValue` and fires `AppraisalFinalValuesChangedEvent`, which rolls
the value up into `appraisal.ValuationAnalyses.AppraisedValue` automatically (the field
`vw_AppraisalList` reads). The path is family-agnostic — one value per group regardless of L/LB/U.

**Run it (after the phase-1 drain has completed):**

1. **Export the group list** — one `groupId` per load-test appraisal:
   ```bash
   docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd \
     -S localhost -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal \
     -W -h -1 < docs/load-test/export-groups.sql > docs/load-test/grouplist.csv
   ```
   Each line is a single `groupId` (no header).

2. **Smoke first** — trim `grouplist.csv` to a single row, then:
   ```bash
   k6 run -e BASE_URL=https://localhost:7111 -e VUS=1 \
          --insecure-skip-tls-verify docs/load-test/fill-pricing.js
   ```
   Expect `complete_pa 200`. Verify the value surfaced:
   `SELECT AppraisedValue, ForcedSaleValue FROM appraisal.ValuationAnalyses WHERE AppraisalId='<aid>'`
   (or just re-query `vw_AppraisalList` for that appraisal).

3. **Full run** — restore the full `grouplist.csv` and:
   ```bash
   k6 run -e BASE_URL=https://localhost:7111 -e VUS=50 \
          --insecure-skip-tls-verify docs/load-test/fill-pricing.js
   ```
   `pricing_completed` should equal the group count; `http_req_failed` ~0.

> **Re-runs:** `create` returns **409** if a group already has a Pricing Analysis. The script
> logs + skips 409s (it can't get the existing id to re-complete), so to redo phase 3 you must
> first clear the analyses (or just re-run the whole pipeline after `cleanup.sql`).

## Reset between runs

```bash
# macOS / no local sqlcmd — run inside the container:
docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal < docs/load-test/cleanup.sql

# or, with sqlcmd installed locally:
sqlcmd -S localhost,1433 -U sa -P 'P@ssw0rd' -C -d CollateralAppraisal -i docs/load-test/cleanup.sql
```

Scoped to `Requestor='loadtest'`, so it won't touch real MANUAL-channel data. Deleting the
requests/appraisals also removes the owned land-detail filled in phase 2 (no extra cleanup needed).
You can also re-run phase 2 idempotently — the PUT overwrites detail in place.
