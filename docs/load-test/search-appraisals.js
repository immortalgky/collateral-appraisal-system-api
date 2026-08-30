// k6 load test: the Appraisal List transaction — GET /appraisals.
//
// One request runs THREE sequential statements on a single (non-MARS) connection:
//   1. SELECT COUNT(*) FROM (SELECT * FROM appraisal.vw_AppraisalList <where>) AS CountQuery
//   2. SELECT * FROM appraisal.vw_AppraisalList <where> ORDER BY <sort> OFFSET .. FETCH ..
//   3. SELECT Status, SLAStatus, Priority, AppraisalType, AssignmentType
//        FROM appraisal.vw_AppraisalList <where>          -- no paging: every matching row
// Measured on ~105k appraisals, statement 2 alone burned ~11 s of SQL CPU to return 20 rows,
// because vw_AppraisalList picks the latest assignment and the first property location with
// ROW_NUMBER() windows filtered by `rn = 1` on the OUTSIDE — the window has to be computed over
// the whole table before the outer WHERE can apply.
//
// AUTH: a REAL bearer token is required, and it must belong to a BANK (internal) user —
// one whose auth.AspNetUsers.CompanyId IS NULL. `X-Dev-Auth: dev-bypass` will NOT do: it stamps
// company_id = Guid.Empty (Shared/Shared/Identity/DevAuthenticationHandler.cs), and
// AppraisalAccessScope then forces a company filter that matches nothing, so every query returns
// 0 rows and the test measures nothing. Get a token with docs/load-test/get-appraisal-token.sh.
//
//   export TOKEN=$(./docs/load-test/get-appraisal-token.sh admin '<password>')
//
//   # 1) Baseline — single-request latency (run FIRST, record p50/p95):
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=1 -e ITERATIONS=30 \
//          --insecure-skip-tls-verify docs/load-test/search-appraisals.js
//
//   # 2) Concurrency — what users actually feel when several search at once:
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=8 -e ITERATIONS=80 \
//          --insecure-skip-tls-verify docs/load-test/search-appraisals.js
//
//   # 3) Capacity — ramp the arrival rate to find the knee:
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=rate -e PEAK_RPS=8 \
//          --insecure-skip-tls-verify docs/load-test/search-appraisals.js
//
//   # 4) One specific filter/sort combination:
//   k6 run ... -e SCENARIO=sort_province
//
import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";

const BASE_URL = (__ENV.BASE_URL || "https://localhost:7111").replace(/\/+$/, "");
const ENDPOINT = __ENV.ENDPOINT || "/appraisals";

// MODE selects the load shape (only one scenario runs at a time):
//   "count" (default) — exactly ITERATIONS requests at concurrency VUS
//   "rate"            — ramp toward PEAK_RPS req/s to find the capacity knee
const MODE = (__ENV.MODE || "count").toLowerCase();

const VUS = parseInt(__ENV.VUS || "8", 10);
const ITERATIONS = parseInt(__ENV.ITERATIONS || "80", 10);

const PEAK_RPS = parseInt(__ENV.PEAK_RPS || "8", 10);
const PRE_VUS = parseInt(__ENV.PRE_VUS || "10", 10);
const MAX_VUS = parseInt(__ENV.MAX_VUS || "100", 10);
const WARMUP = __ENV.WARMUP || "30s";
const STAGE_DUR = __ENV.STAGE_DUR || "1m";

// PageSize 25 is the FE default (Pagination.tsx offers 10/25/50/100).
const PAGE_SIZE = parseInt(__ENV.PAGE_SIZE || "25", 10);

// p(95) budget in ms for a single list request.
const P95_MS = parseInt(__ENV.P95_MS || "2000", 10);

// The query shapes the FE actually produces, each with a share of the traffic.
//
// Weights matter more than they look: sorting by appointmentDateTime is ~10x the cost of every
// other shape, so drawing all shapes uniformly puts it on ~9% of requests and it alone dictates the
// p95 of the whole run. That measures the worst case, not the service.
//
// These weights are an ASSUMPTION about how the page is used, derived from the UI rather than from
// traffic: AppraisalListPage lands on the default sort with no filter, the status chips are the
// primary filter affordance, search is debounced so it fires once per pause, and sorting by a
// column is a deliberate act most sessions never perform. Replace them with real numbers as soon as
// UAT/production query-string logs are available — or override per run:
//
//   -e WEIGHTS="default:50,free_text:10,sort_appointment:0"
//
// PageNumber is 0-based (Shared/Shared/Pagination/PaginationRequest.cs).
const CASES = [
  // Opening the page, and paging through it.
  { name: "default", weight: 30, q: {} },
  { name: "deep_page", weight: 2, q: { status: "Pending", pageNumber: 500 } },
  // Filtering — the status chips and the filter panel.
  { name: "status_pending", weight: 20, q: { status: "Pending" } },
  { name: "status_multi", weight: 8, q: { status: "Pending,InProgress,Completed" } },
  { name: "status_narrow", weight: 5, q: { status: "Completed" } },
  { name: "priority_type", weight: 5, q: { priority: "Normal", appraisalType: "New" } },
  // Free-text box (debounced 300 ms, so roughly one request per pause in typing).
  { name: "free_text", weight: 15, q: { search: "REQ-105" } },
  // Clicking a column header. Deliberate, and rarer than filtering.
  { name: "sort_sla", weight: 5, q: { sortBy: "slaStatus", sortDir: "asc" } },
  { name: "sort_customer", weight: 4, q: { sortBy: "customerName", sortDir: "asc" } },
  { name: "sort_province", weight: 3, q: { sortBy: "province", sortDir: "asc" } },
  { name: "sort_appointment", weight: 3, q: { sortBy: "appointmentDateTime", sortDir: "desc" } },
];

// WEIGHTS overrides named shapes; anything not named keeps its default. A weight of 0 removes the
// shape from the draw entirely.
const WEIGHT_OVERRIDES = (__ENV.WEIGHTS || "")
  .split(",")
  .map((pair) => pair.trim())
  .filter(Boolean)
  .reduce((acc, pair) => {
    const [name, value] = pair.split(":").map((part) => part.trim());
    const weight = Number(value);
    if (!name || !Number.isFinite(weight) || weight < 0) {
      throw new Error(`Bad WEIGHTS entry "${pair}" — expected name:number, e.g. free_text:10`);
    }
    acc[name] = weight;
    return acc;
  }, {});

for (const name of Object.keys(WEIGHT_OVERRIDES)) {
  if (!CASES.some((c) => c.name === name)) {
    throw new Error(`Unknown shape "${name}" in WEIGHTS. Known: ${CASES.map((c) => c.name).join(", ")}`);
  }
}

// SCENARIO pins a single shape (weights stop mattering) — use it to profile one query in isolation.
const SCENARIO = (__ENV.SCENARIO || "").trim();
if (SCENARIO && !CASES.some((c) => c.name === SCENARIO)) {
  throw new Error(`Unknown SCENARIO "${SCENARIO}". Known: ${CASES.map((c) => c.name).join(", ")}`);
}

const ACTIVE = (SCENARIO ? CASES.filter((c) => c.name === SCENARIO) : CASES)
  .map((c) => ({ ...c, weight: SCENARIO ? 1 : (WEIGHT_OVERRIDES[c.name] ?? c.weight) }))
  .filter((c) => c.weight > 0);

if (ACTIVE.length === 0) {
  throw new Error("Every shape has weight 0 — nothing to run.");
}

// Cumulative weights, so one random number picks a shape in a single pass.
const TOTAL_WEIGHT = ACTIVE.reduce((sum, c) => sum + c.weight, 0);
const CUMULATIVE = [];
ACTIVE.reduce((running, c) => {
  const next = running + c.weight;
  CUMULATIVE.push(next);
  return next;
}, 0);

function pickShape() {
  const roll = Math.random() * TOTAL_WEIGHT;
  for (let i = 0; i < CUMULATIVE.length; i++) {
    if (roll < CUMULATIVE[i]) return ACTIVE[i];
  }
  return ACTIVE[ACTIVE.length - 1]; // floating-point guard
}

const TOKEN = __ENV.TOKEN || "";
if (!TOKEN) {
  throw new Error(
    "TOKEN is required: pass -e TOKEN=\"<jwt>\". It must belong to a bank/internal user " +
      "(auth.AspNetUsers.CompanyId IS NULL); dev-bypass is company-scoped and returns 0 rows."
  );
}
const HEADERS = {
  Accept: "application/json",
  Authorization: TOKEN.toLowerCase().startsWith("bearer ") ? TOKEN : `Bearer ${TOKEN}`,
};

const searched = new Counter("appraisal_searches");
const emptyResults = new Counter("empty_results");

const scenarios =
  MODE === "rate"
    ? {
        search_rate: {
          executor: "ramping-arrival-rate",
          startRate: Math.max(1, Math.round(PEAK_RPS * 0.25)),
          timeUnit: "1s",
          preAllocatedVUs: PRE_VUS,
          maxVUs: MAX_VUS,
          stages: [
            { target: Math.max(1, Math.round(PEAK_RPS * 0.5)), duration: WARMUP },
            { target: PEAK_RPS, duration: STAGE_DUR },
            { target: PEAK_RPS * 2, duration: STAGE_DUR },
            { target: PEAK_RPS * 4, duration: STAGE_DUR },
            { target: 0, duration: "30s" },
          ],
        },
      }
    : {
        search_count: {
          executor: "shared-iterations",
          vus: VUS,
          iterations: ITERATIONS,
          maxDuration: "1h",
        },
      };

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: scenarios,
  thresholds: {
    http_req_failed: ["rate<0.01"],
    "http_req_duration{name:search_appraisal}": [`p(95)<${P95_MS}`],
    checks: ["rate>0.99"],
  },
};

function urlFor(testCase) {
  const q = Object.assign({ pageNumber: 0, pageSize: PAGE_SIZE }, testCase.q);
  const params = Object.keys(q).map((k) => `${k}=${encodeURIComponent(q[k])}`);
  return `${BASE_URL}${ENDPOINT}?${params.join("&")}`;
}

export function setup() {
  const mix = ACTIVE
    .map((c) => `${c.name} ${((c.weight / TOTAL_WEIGHT) * 100).toFixed(1)}%`)
    .join("  ");
  console.log(`shape mix: ${mix}`);
}

export default function () {
  const testCase = pickShape();
  const res = http.get(urlFor(testCase), {
    headers: HEADERS,
    // Two tags: one aggregate for the threshold, one per case so the summary breaks down by shape.
    tags: { name: "search_appraisal", shape: testCase.name },
  });

  const ok = check(res, {
    "list 200": (r) => r.status === 200,
    // A token that is company-scoped silently returns an empty page — that would make every
    // timing meaningless, so fail loudly instead of reporting a fast zero.
    "list has rows": (r) => {
      if (r.status !== 200) return false;
      try {
        const body = r.json();
        const count = body && body.result ? body.result.count : 0;
        if (count === 0) {
          emptyResults.add(1);
          // deep_page/status_narrow can legitimately be empty; everything else cannot.
          return testCase.name === "deep_page" || testCase.name === "status_narrow";
        }
        return true;
      } catch (e) {
        return false;
      }
    },
  });

  if (ok) {
    searched.add(1);
  } else {
    console.error(`[${testCase.name}] ${res.status} ${String(res.body).slice(0, 300)}`);
  }
}
