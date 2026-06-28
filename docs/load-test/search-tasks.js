// k6 load test: the "SearchTask" transaction — GET /tasks/pool with a free-text search.
//
// This is the read-side endpoint the load-test report flagged at 7.6-9.6s. The slow path is
// the `search` / `customerName` filter: a leading-wildcard `LIKE '%term%'` over the heavy
// workflow.vw_TaskList view (4-branch UNION ALL + cross-schema OUTER APPLYs), with the view
// run TWICE per request (count + page) when an "enriched" filter is active. Pool is the slow
// one because it scans the whole group/team pool; /tasks/me pre-filters to one user and stays
// fast. Point ENDPOINT at /tasks/me to compare.
//
// AUTH: unlike the create-submit phases, this needs a REAL bearer token — the pool query
// scopes rows by the caller's group/team/company, so dev-bypass wouldn't exercise the right
// candidate set. Pass it via TOKEN (no "Bearer " prefix needed; added if missing).
//
//   # 1) Baseline — single request latency (run this FIRST, get p50/p95):
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=1 -e ITERATIONS=100 \
//          --insecure-skip-tls-verify docs/load-test/search-tasks.js
//
//   # 2) Concurrency — fixed VUs, exact iteration count (reproduces the report's contention):
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=20 -e ITERATIONS=500 \
//          --insecure-skip-tls-verify docs/load-test/search-tasks.js
//
//   # 3) Capacity / stress — ramp the request rate to find the knee:
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=rate -e PEAK_RPS=20 \
//          --insecure-skip-tls-verify docs/load-test/search-tasks.js
//
//   # 4) Typing simulation — mimic the FE firing one search per keystroke (P, Pe, Per, ...):
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=10 -e ITERATIONS=200 -e TYPE=true -e SEARCH=Performance \
//          --insecure-skip-tls-verify docs/load-test/search-tasks.js

import http from "k6/http";
import { check, sleep } from "k6";
import { Counter } from "k6/metrics";

// ---- config (env-driven) ----
const BASE_URL = (__ENV.BASE_URL || "https://localhost:7111").replace(/\/+$/, "");

// Endpoint under test. Default = the slow pool search; switch to /tasks/me to compare.
const ENDPOINT = __ENV.ENDPOINT || "/tasks/pool";

// MODE selects the load shape (only one scenario runs at a time):
//   "count" (default) — exact ITERATIONS requests at concurrency VUS
//   "rate"            — ramp toward PEAK_RPS req/s to find the capacity knee
const MODE = (__ENV.MODE || "count").toLowerCase();

// count-mode knobs
const VUS = parseInt(__ENV.VUS || "10", 10);
const ITERATIONS = parseInt(__ENV.ITERATIONS || "300", 10);

// rate-mode knobs
const PEAK_RPS = parseInt(__ENV.PEAK_RPS || "20", 10);
const PRE_VUS = parseInt(__ENV.PRE_VUS || "20", 10);
const MAX_VUS = parseInt(__ENV.MAX_VUS || "200", 10);
const WARMUP = __ENV.WARMUP || "1m";
const STAGE_DUR = __ENV.STAGE_DUR || "3m";

// Query knobs (must mirror what the FE sends — see GetPoolTasksEndpoint params).
// PaginationRequest.PageNumber is 0-based.
const PAGE_SIZE = parseInt(__ENV.PAGE_SIZE || "25", 10);
const SORT_BY = __ENV.SORT_BY || ""; // empty => server default (AssignedDate DESC)
const SORT_DIR = __ENV.SORT_DIR || "desc";

// Search behavior:
//   SEARCH       — base term typed into the top search box (matches AppraisalNumber OR CustomerName)
//   SEARCH_FIELD — "search" (default, the top box) or "customerName" (the explicit column filter)
//   TYPE=true    — simulate typing: fire one request per growing prefix of SEARCH (P, Pe, Per, ...)
//                  with THINK_MS between keystrokes, reproducing the report's per-keystroke calls.
//   VARY=true    — append a short random suffix per request so terms differ run-to-run.
const SEARCH = __ENV.SEARCH || "a";
const SEARCH_FIELD = (__ENV.SEARCH_FIELD || "search").trim();
const TYPE = (__ENV.TYPE || "false").toLowerCase() === "true";
const THINK_MS = parseInt(__ENV.THINK_MS || "150", 10);
const VARY = (__ENV.VARY || "false").toLowerCase() === "true";

// AUTH — bearer token is required (the pool query is scoped to the caller's groups/team).
const TOKEN = __ENV.TOKEN || "";
if (!TOKEN) {
  throw new Error(
    "TOKEN is required: pass -e TOKEN=\"<jwt>\" (the access token from an authenticated user). " +
      "The /tasks/pool query scopes rows by the caller's groups/team, so a real token is needed."
  );
}
const HEADERS = {
  Accept: "application/json",
  Authorization: TOKEN.toLowerCase().startsWith("bearer ") ? TOKEN : `Bearer ${TOKEN}`,
};

const searched = new Counter("task_searches");

const scenarios =
  MODE === "rate"
    ? {
        search_rate: {
          executor: "ramping-arrival-rate",
          startRate: Math.max(1, Math.round(PEAK_RPS * 0.2)),
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
    // The whole point of the test: how slow is the search request? Reported >9s; we want <2s.
    "http_req_duration{name:search_task}": ["p(95)<2000"],
    checks: ["rate>0.99"],
  },
};

function rand4() {
  return Math.random().toString(36).slice(2, 6);
}

// Builds the query string for one search request with a given term.
function urlFor(term) {
  const params = [
    `PageNumber=0`,
    `PageSize=${PAGE_SIZE}`,
    `${SEARCH_FIELD}=${encodeURIComponent(term)}`,
    `SortDir=${encodeURIComponent(SORT_DIR)}`,
  ];
  if (SORT_BY) params.push(`SortBy=${encodeURIComponent(SORT_BY)}`);
  return `${BASE_URL}${ENDPOINT}?${params.join("&")}`;
}

function fireSearch(term) {
  const res = http.get(urlFor(term), { headers: HEADERS, tags: { name: "search_task" } });
  const ok = check(res, {
    "search 200": (r) => r.status === 200,
  });
  if (ok) {
    searched.add(1);
  } else {
    console.error(`search failed (${term}): ${res.status} ${String(res.body).slice(0, 300)}`);
  }
  return res;
}

export default function () {
  const base = VARY ? `${SEARCH}${rand4()}` : SEARCH;

  if (TYPE) {
    // Simulate the FE firing a request per keystroke as the user types `base`.
    for (let i = 1; i <= base.length; i++) {
      fireSearch(base.slice(0, i));
      if (THINK_MS > 0) sleep(THINK_MS / 1000);
    }
  } else {
    fireSearch(base);
  }
}
