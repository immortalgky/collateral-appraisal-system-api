// k6 load test: the navbar quick-search — GET /search.
//
// Sibling of search-appraisals.js, which drives the Appraisal List (GET /appraisals). The two share
// a root: QuickSearchQueryHandler joins appraisal.vw_AppraisalList, the same view whose ROW_NUMBER()
// windows are filtered on the OUTSIDE (`rn = 1`), so the window is computed over the whole table
// before the outer WHERE can apply. Whatever that costs the list, it costs this too — run both and
// compare, rather than assuming the cheap-looking endpoint is cheap.
//
// One request runs a single batch: the LIKE arms materialise into #m once, then #m is read twice
// (QuickSearchQueryHandler's own note explains why a CTE would re-evaluate all 17 arms). The handler
// documents 44 ms with OPTION (RECOMPILE) and 223-241 ms without, measured on the dev database — if
// this test reports seconds, something has moved since.
//
// TERM SHAPE IS THE WHOLE EXPERIMENT. The handler treats a term three ways:
//   plain      -> prefix match  (`abc%`)   — the indexed, fast path
//   *-prefixed -> substring     (`%abc%`)  — a deliberate opt-in, scans
//   % or _     -> escaped and matched literally, NOT as a wildcard
// A run that only sends prefixes measures the best case and says nothing about what a user typing
// `*smith` costs. The cases below cover all three; weights decide which one your p95 reflects.
//
// AUTH: a REAL bearer token is required, and it must belong to a BANK (internal) user — one whose
// auth.AspNetUsers.CompanyId IS NULL. `X-Dev-Auth: dev-bypass` will NOT do: it stamps
// company_id = Guid.Empty (Shared/Shared/Identity/DevAuthenticationHandler.cs) and the results are
// then scoped to a company that matches nothing, so the run measures an empty set and looks fast.
//
//   export TOKEN=$(./docs/load-test/get-appraisal-token.sh admin '<password>')
//
//   # 1) Baseline — single-request latency (run FIRST, record p50/p95):
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=1 -e ITERATIONS=30 \
//          --insecure-skip-tls-verify docs/load-test/search-quick.js
//
//   # 2) Concurrency — what users feel when several type at once (the navbar fires per keystroke
//   #    pause, so real concurrency here is higher than on the list page):
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=count -e VUS=8 -e ITERATIONS=80 \
//          --insecure-skip-tls-verify docs/load-test/search-quick.js
//
//   # 3) Capacity — ramp the arrival rate to find the knee:
//   k6 run -e BASE_URL=https://localhost:7111 -e TOKEN="$TOKEN" \
//          -e MODE=rate -e PEAK_RPS=8 \
//          --insecure-skip-tls-verify docs/load-test/search-quick.js
//
//   # 4) One shape only — e.g. prove what substring search costs:
//   k6 run ... -e SCENARIO=substring
//
//   # 5) Re-weight the mix, or replay an earlier run exactly:
//   k6 run ... -e WEIGHTS="prefix_number:50,substring:50" -e SEED=42
//
// TERMS: the defaults below are PREFIXES THAT EXIST on the dev database (appraisal numbers start
// 69…). A term that matches nothing exercises the miss path, which is not what you want to measure.
// Override with real traffic as soon as you have it:
//
//   -e TERM_NUMBER=69105 -e TERM_CUSTOMER=บริษัท -e TERM_PROPERTY=โฉนด
//
import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";
import {
  buildScenarios,
  authHeaders,
  listThresholds,
  trimTrailingSlashes,
  buildShapePicker,
  scenarioOptionsFromEnv,
} from "./lib/k6-common.js";

const BASE_URL = trimTrailingSlashes(__ENV.BASE_URL || "https://localhost:7111");
const ENDPOINT = __ENV.ENDPOINT || "/search";

// MODE / VUS / ITERATIONS / PEAK_RPS / PRE_VUS / MAX_VUS / WARMUP / STAGE_DUR select the load
// shape — "count" runs exactly ITERATIONS requests at concurrency VUS, "rate" ramps toward PEAK_RPS
// to find the capacity knee. See scenarioOptionsFromEnv in lib/k6-common.js for the defaults.

// The navbar asks for 8 (QuickSearchEndpoint's default when limit is absent).
const LIMIT = Number.parseInt(__ENV.LIMIT || "8", 10);

// p(95) budget in ms for one quick-search request. Tighter than the list's 2000: this fires while
// the user is still typing, so anything above a few hundred ms reads as a broken box rather than a
// slow page.
const P95_MS = Number.parseInt(__ENV.P95_MS || "800", 10);

// Terms that MATCH on the target database. See the note above about miss paths.
// Three characters minimum — AppraisalSearchPredicate.MinTermLength — or the endpoint answers 400
// in under a millisecond and the run measures the validation branch instead of the query.
const TERM_NUMBER = __ENV.TERM_NUMBER || "690";
const TERM_CUSTOMER = __ENV.TERM_CUSTOMER || "นาย";
const TERM_PROPERTY = __ENV.TERM_PROPERTY || "กรุงเทพ";

// The shapes the navbar actually produces, each with a share of the traffic.
//
// These weights are an ASSUMPTION derived from the UI, not from traffic: the box is a number-first
// affordance (staff paste an appraisal or request number far more often than they browse by name),
// scope is left at "all" unless someone opens the filter, and the `*` substring opt-in is a power
// user's habit. Replace them the moment UAT/production query logs exist — or override per run:
//
//   -e WEIGHTS="prefix_number:50,substring:50"
//
const CASES = [
  // The common case: a number typed or pasted into the box, scope left alone.
  { name: "prefix_number", weight: 40, q: { q: TERM_NUMBER } },

  // Names — the second-most-typed thing, and the arm that reaches the customer tables.
  { name: "prefix_customer", weight: 20, q: { q: TERM_CUSTOMER, scope: "customers" } },

  // Collateral — reaches the property/address arms, which resolve against the address masters.
  { name: "prefix_property", weight: 15, q: { q: TERM_PROPERTY, scope: "properties" } },

  // Documents scope: title deeds and document numbers.
  { name: "scope_documents", weight: 10, q: { q: TERM_NUMBER, scope: "documents" } },

  // The deliberate opt-in to substring matching. Expected to be the expensive shape — a leading
  // wildcard cannot use an index — which is exactly why it is worth measuring separately rather
  // than letting it hide inside an average.
  { name: "substring", weight: 10, q: { q: `*${TERM_NUMBER}` } },

  // A term made of SQL wildcards. Must be escaped and matched literally: if this ever returns the
  // whole table it is both a correctness bug and the slowest query the endpoint can be made to run.
  // Kept at a low weight — it is a guard, not a representative shape.
  { name: "literal_wildcard", weight: 5, q: { q: "%%%" } },
];

// WEIGHTS re-weights named shapes (0 removes one), SCENARIO pins a single shape, SEED replays a run
// shape-for-shape. See buildShapePicker in lib/k6-common.js.
const pickCase = buildShapePicker(CASES, {
  weights: __ENV.WEIGHTS || "",
  scenario: __ENV.SCENARIO || "",
  seed: __ENV.SEED || "",
});

// Counted per case so a run tells you WHICH shape was slow, not just that something was. k6's own
// per-name http_req_duration gives the latency; these give the mix and the empty-result rate.
const requestsByCase = new Counter("quick_search_requests");
const emptyByCase = new Counter("quick_search_empty_results");

export const options = {
  scenarios: buildScenarios(scenarioOptionsFromEnv(__ENV, "quick_search")),
  thresholds: listThresholds("quick_search", P95_MS),
  insecureSkipTLSVerify: true,
};

const HEADERS = authHeaders(
  __ENV.TOKEN,
  "Quick search requires an internal (CompanyId IS NULL) bank user; dev-bypass scopes to an empty company."
);

function buildUrl(params) {
  const search = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");
  return `${BASE_URL}${ENDPOINT}?${search}&limit=${LIMIT}`;
}

export default function quickSearch() {
  const testCase = pickCase();
  const url = buildUrl(testCase.q);

  const res = http.get(url, {
    headers: HEADERS,
    tags: { name: "quick_search", shape: testCase.name },
  });

  requestsByCase.add(1, { shape: testCase.name });

  const ok = check(
    res,
    {
      "status 200": (r) => r.status === 200,
      // A shape that returns nothing is measuring the miss path. Not a failure — the literal
      // wildcard case is expected to be empty — but it has to be visible, or a run that matched
      // nothing looks like a fast one.
      // QuickSearchResult is { groups, hasMore, totalMatchedAppraisals, isTotalApproximate } —
      // one group per matched VALUE, each carrying the appraisals that share it. Counting groups
      // rather than rows is what the navbar shows.
      "body parses": (r) => {
        if (r.status !== 200) return false;
        try {
          const groups = r.json()?.groups?.length ?? 0;
          if (groups === 0) emptyByCase.add(1, { shape: testCase.name });
          return true;
        } catch {
          // A 200 whose body is not JSON is a failed check, not a crash: returning false records it
          // against this shape and lets the run carry on measuring the rest.
          return false;
        }
      },
    },
    { shape: testCase.name }
  );

  if (!ok && res.status !== 200) {
    // One line, not the whole body: a 500 here is usually a SQL timeout and the detail is the part
    // worth reading.
    console.error(`${testCase.name} -> ${res.status}: ${String(res.body).slice(0, 200)}`);
  }
}
