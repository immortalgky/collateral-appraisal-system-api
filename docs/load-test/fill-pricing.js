// k6 PHASE 3: give each appraisal a CONCLUDED VALUE via Pricing Analysis.
// Goal = realistic data at volume (NOT throughput). Pricing Analysis is per
// PropertyGroup; phase 1 auto-creates one "Group 1" per appraisal. The minimal
// path to a persisted appraised value is 3 sequential calls per group (no
// approaches/methods needed):
//
//   1) POST /property-groups/{groupId}/pricing-analysis        -> 200 { id, status:"Draft" }
//   2) POST /pricing-analysis/{id}/start                        -> 200 { status:"InProgress" }
//   3) POST /pricing-analysis/{id}/complete  {MarketValue,..}   -> 200 { status:"Completed" }
//
// Complete() sets FinalAppraisedValue and fires AppraisalFinalValuesChangedEvent,
// which rolls the value up into appraisal.ValuationAnalyses.AppraisedValue — the
// field vw_AppraisalList shows as AppraisalValue.
//
// Run AFTER the phase-1 drain completes and AFTER exporting the group list:
//   docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'P@ssw0rd' \
//     -C -d CollateralAppraisal -W -h -1 < docs/load-test/export-groups.sql \
//     > docs/load-test/grouplist.csv
//   k6 run -e BASE_URL=https://localhost:7111 -e VUS=50 \
//          --insecure-skip-tls-verify docs/load-test/fill-pricing.js
//
// NOTE: create returns 409 if the group already has a PricingAnalysis, so a
// re-run needs a cleanup first (the script logs+skips 409s).

import http from "k6/http";
import { check } from "k6";
import exec from "k6/execution";
import { SharedArray } from "k6/data";
import { Counter } from "k6/metrics";
import { randomPrice } from "./pools.js";

// ---- config (env-driven) ----
const BASE_URL = (__ENV.BASE_URL || "https://localhost:7111").replace(/\/+$/, "");
const VUS = parseInt(__ENV.VUS || "50", 10);
const GROUPLIST = __ENV.GROUPLIST || "./grouplist.csv";

const HEADERS = {
  "Content-Type": "application/json",
  "X-Dev-Auth": "dev-bypass",
};

// One groupId per non-empty line (no header; see export-groups.sql).
const groups = new SharedArray("grouplist", function () {
  const raw = open(GROUPLIST);
  const rows = [];
  for (const line of raw.split("\n")) {
    const t = line.trim();
    if (t) rows.push(t);
  }
  return rows;
});

const completed = new Counter("pricing_completed");

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    fill_pricing: {
      executor: "shared-iterations",
      vus: VUS,
      iterations: groups.length,
      maxDuration: "8h",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    "http_req_duration{name:complete_pa}": ["p(95)<2000"],
    checks: ["rate>0.99"],
  },
};

export default function () {
  const groupId = groups[exec.scenario.iterationInTest];
  if (!groupId) return;

  // 1) create
  const create = http.post(`${BASE_URL}/property-groups/${groupId}/pricing-analysis`, null, {
    headers: HEADERS,
    tags: { name: "create_pa" },
  });
  if (create.status !== 200) {
    // 409 = a PricingAnalysis already exists for this group (re-run without cleanup).
    console.error(`create_pa ${groupId}: ${create.status} ${create.body}`);
    return;
  }
  const paId = create.json("id");
  if (!paId) {
    console.error(`create_pa ${groupId}: no id in response ${create.body}`);
    return;
  }

  // 2) start (Draft -> InProgress)
  const start = http.post(`${BASE_URL}/pricing-analysis/${paId}/start`, null, {
    headers: HEADERS,
    tags: { name: "start_pa" },
  });
  check(start, { "start_pa 200": (r) => r.status === 200 });

  // 3) complete with a realistic value (InProgress -> Completed)
  const appraised = randomPrice(); // 0.5M–30M, rounded to 10k
  const body = {
    marketValue: appraised,
    appraisedValue: appraised,
    forcedSaleValue: null, // server derives ~0.70 * appraised in the rollup
  };
  const complete = http.post(`${BASE_URL}/pricing-analysis/${paId}/complete`, JSON.stringify(body), {
    headers: HEADERS,
    tags: { name: "complete_pa" },
  });

  const ok = check(complete, { "complete_pa 200": (r) => r.status === 200 });
  if (ok) {
    completed.add(1);
  } else {
    console.error(`complete_pa ${groupId}/${paId}: ${complete.status} ${complete.body}`);
  }
}
