// k6 end-to-end load test: create+submit ~100k appraisal applications through the
// REAL pipeline. POST /api/v1/requests (Integration module) creates AND submits in a
// single call (CreateAndSubmitRequestAsync, entrySource "API") -> domain event -> outbox
// -> RabbitMQ -> Workflow consumer -> Appraisal consumer. The HTTP call returns once the
// event is in the outbox; the appraisal is created asynchronously (watch the drain with
// monitor.sql + the RabbitMQ UI).
//
// Two modes (env-selected via MODE; only one scenario runs):
//
//   count (default) — exact TARGET applications at concurrency VUS (data volume / drain):
//     k6 run -e BASE_URL=https://localhost:7111 -e TARGET=100000 -e VUS=50 \
//            --insecure-skip-tls-verify docs/load-test/create-submit.js
//
//   rate — production-grade ramp toward PEAK_RPS req/s (find the capacity knee):
//     k6 run -e MODE=rate -e PEAK_RPS=100 -e BASE_URL=https://localhost:7111 \
//            --insecure-skip-tls-verify docs/load-test/create-submit.js
//
//   smoke (1 application):
//     k6 run -e BASE_URL=https://localhost:7111 -e TARGET=1 -e VUS=1 \
//            --insecure-skip-tls-verify docs/load-test/create-submit.js

import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";
import {
  randomGeo,
  randomName,
  randomPhone,
  randomPrice,
  randDigits,
  pick,
  BANKING_SEGMENTS,
  PRIORITIES,
  COLLATERAL_TYPES,
} from "./pools.js";

// ---- config (env-driven) ----
const BASE_URL = (__ENV.BASE_URL || "https://localhost:7111").replace(/\/+$/, "");
const API = `${BASE_URL}/api/v1`;

// MODE selects the load shape (only one scenario runs at a time):
//   "count" (default) — exact TARGET applications at concurrency VUS (data-volume / drain test)
//   "rate"            — production-grade ramp toward PEAK_RPS req/s (find the capacity knee)
const MODE = (__ENV.MODE || "count").toLowerCase();

// count-mode knobs
const TARGET = parseInt(__ENV.TARGET || "100000", 10); // total applications (exact)
const VUS = parseInt(__ENV.VUS || "50", 10); // concurrent virtual users

// rate-mode knobs
const PEAK_RPS = parseInt(__ENV.PEAK_RPS || "100", 10); // expected peak requests/sec
const PRE_VUS = parseInt(__ENV.PRE_VUS || "50", 10); // VUs pre-allocated for the ramp
const MAX_VUS = parseInt(__ENV.MAX_VUS || "500", 10); // upper bound k6 may allocate
const WARMUP = __ENV.WARMUP || "2m"; // warm-up stage duration
const STAGE_DUR = __ENV.STAGE_DUR || "5m"; // duration of each load stage

// Marker so generated data is identifiable + deletable (see cleanup.sql).
const MARKER = "loadtest";

// dev-bypass auth — no OAuth token needed in dev.
const HEADERS = {
  "Content-Type": "application/json",
  "X-Dev-Auth": "dev-bypass",
};

const submitted = new Counter("applications_submitted");

// Build exactly one scenario based on MODE.
const scenarios =
  MODE === "rate"
    ? {
        // ramping-arrival-rate holds a requests/sec target and auto-allocates VUs to
        // sustain it. Stages: warm up -> expected peak -> 2x peak -> 4x peak (stress) ->
        // ramp down. Watch p95, http_req_failed, and the outbox backlog (monitor.sql):
        // the knee is where any of them breaks. `dropped_iterations` > 0 means k6 (or the
        // system) couldn't keep up with the target rate — raise MAX_VUS or it's the limit.
        prod_load: {
          executor: "ramping-arrival-rate",
          startRate: Math.max(1, Math.round(PEAK_RPS * 0.1)),
          timeUnit: "1s",
          preAllocatedVUs: PRE_VUS,
          maxVUs: MAX_VUS,
          stages: [
            { target: Math.max(1, Math.round(PEAK_RPS * 0.5)), duration: WARMUP },
            { target: PEAK_RPS, duration: STAGE_DUR },
            { target: PEAK_RPS * 2, duration: STAGE_DUR },
            { target: PEAK_RPS * 4, duration: STAGE_DUR },
            { target: 0, duration: "1m" },
          ],
        },
      }
    : {
        // shared-iterations gives an EXACT total of TARGET applications across VUS workers,
        // with concurrency = VUS.
        create_submit: {
          executor: "shared-iterations",
          vus: VUS,
          iterations: TARGET,
          maxDuration: "8h",
        },
      };

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: scenarios,
  thresholds: {
    // overall error budget
    http_req_failed: ["rate<0.01"],
    // create+submit latency (single call)
    "http_req_duration{name:create_request}": ["p(95)<2000"],
    checks: ["rate>0.99"],
  },
};

// Builds one realistic, varied create-request payload.
// FIXED per decision: purpose "01", channel "MANUAL", session null.
// VARIED: collateral type (01/02/08 -> property family L/LB/U), synchronized geo triple,
// customer/owner names + phones, amounts, appointment date.
function buildCreatePayload() {
  const geo = randomGeo();
  const titleGeo = randomGeo();
  const collateral = pick(COLLATERAL_TYPES); // { code, family }
  const sellingPrice = randomPrice();
  const facilityLimit = Math.round(sellingPrice * 0.8);

  // appointment 1–30 days out
  const appt = new Date();
  appt.setDate(appt.getDate() + 1 + Math.floor(Math.random() * 30));
  const apptStr = appt.toISOString().slice(0, 19); // "YYYY-MM-DDTHH:mm:ss"

  const address = {
    houseNumber: `${1 + Math.floor(Math.random() * 999)}/${Math.floor(Math.random() * 99)}`,
    roomNumber: "",
    floorNumber: "",
    buildingNumber: "",
    projectName: "",
    moo: "",
    soi: "",
    road: "",
    subDistrict: geo.subDistrict,
    district: geo.district,
    province: geo.province,
    postcode: geo.postcode,
  };

  return {
    uploadSessionId: null,
    purpose: "01",
    channel: "MANUAL",
    requestor: { userId: MARKER, username: "Load Test" },
    creator: { userId: MARKER, username: "Load Test" },
    priority: pick(PRIORITIES),
    isPma: false,
    detail: {
      hasAppraisalBook: false,
      loanDetail: {
        bankingSegment: pick(BANKING_SEGMENTS),
        loanApplicationNumber: `LT-${randDigits(10)}`,
        facilityLimit: facilityLimit,
        additionalFacilityLimit: null,
        previousFacilityLimit: null,
        totalSellingPrice: sellingPrice,
      },
      prevAppraisalId: null,
      address: address,
      contact: {
        contactPersonName: randomName(),
        contactPersonPhone: randomPhone(),
        dealerCode: `D${randDigits(4)}`,
      },
      appointment: {
        appointmentDateTime: apptStr,
        appointmentLocation: `Site ${randDigits(3)}`,
      },
      fee: {
        feePaymentType: "01",
        feeNotes: "load-test",
        AbsorbedAmount: null,
      },
    },
    customers: [
      { name: randomName(), contactNumber: randomPhone() },
    ],
    properties: [
      { propertyType: collateral.family, buildingType: "01", sellingPrice: sellingPrice },
    ],
    documents: [],
    comments: [],
    titles: [
      {
        collateralType: collateral.code,
        collateralStatus: true,
        titleNumber: `LT-${randDigits(8)}`,
        titleType: "DEED",
        landParcelNumber: randDigits(4),
        surveyNumber: randDigits(4),
        areaRai: Math.floor(Math.random() * 10),
        areaNgan: Math.floor(Math.random() * 4),
        areaSquareWa: Math.floor(Math.random() * 100),
        ownerName: randomName(),
        titleAddress: {
          houseNumber: `${1 + Math.floor(Math.random() * 999)}`,
          projectName: "",
          moo: "",
          soi: "",
          road: "",
          subDistrict: titleGeo.subDistrict,
          district: titleGeo.district,
          province: titleGeo.province,
          postcode: titleGeo.postcode,
        },
        dopaAddress: {
          houseNumber: `${1 + Math.floor(Math.random() * 999)}`,
          projectName: "",
          moo: "",
          soi: "",
          road: "",
          subDistrict: titleGeo.subDistrict,
          district: titleGeo.district,
          province: titleGeo.province,
          postcode: titleGeo.postcode,
        },
        notes: "load-test",
        documents: [],
      },
    ],
  };
}

export default function () {
  // Single call: POST /api/v1/requests creates AND submits (entrySource "API"),
  // publishing the integration event that drives the bus.
  const res = http.post(`${API}/requests`, JSON.stringify(buildCreatePayload()), {
    headers: HEADERS,
    tags: { name: "create_request" },
  });

  const ok = check(res, {
    "create+submit 201": (r) => r.status === 201,
  });
  if (ok) {
    submitted.add(1);
  } else {
    // surface the server message to make a misconfig (e.g. doc-validation 400) obvious
    console.error(`create+submit failed: ${res.status} ${res.body}`);
  }
}
