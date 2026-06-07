// k6 PHASE 2: fill property DETAIL for the appraisals phase 1 created.
// Goal = realistic data at volume (NOT throughput). Each work-list row is an
// (appraisalId, propertyId, propertyType) triple exported by export-worklist.sql;
// we PUT a realistic body to the (un-gated) update endpoint matching the family:
//
//   L  -> PUT /appraisals/{aid}/properties/{pid}/land-detail
//   LB -> PUT /appraisals/{aid}/properties/{pid}/land-and-building-detail
//   U  -> PUT /appraisals/{aid}/properties/{pid}/condo-detail
//   (all return 204 No Content)
//
// Run AFTER phase-1 drain completes and AFTER exporting the work-list:
//   docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'P@ssw0rd' \
//     -C -d CollateralAppraisal -s"," -W -h -1 < docs/load-test/export-worklist.sql \
//     > docs/load-test/worklist.csv
//   k6 run -e BASE_URL=https://localhost:7111 -e VUS=50 \
//          --insecure-skip-tls-verify docs/load-test/fill-detail.js
//
// Smoke (single row): export a 1-row worklist.csv, then run with -e VUS=1.

import http from "k6/http";
import { check } from "k6";
import exec from "k6/execution";
import { SharedArray } from "k6/data";
import { Counter } from "k6/metrics";
import { randomGeo, randomName, randDigits } from "./pools.js";

// ---- config (env-driven) ----
const BASE_URL = (__ENV.BASE_URL || "https://localhost:7111").replace(/\/+$/, "");
const VUS = parseInt(__ENV.VUS || "50", 10);
const WORKLIST = __ENV.WORKLIST || "./worklist.csv";

// dev-bypass auth — no OAuth token needed in dev.
const HEADERS = {
  "Content-Type": "application/json",
  "X-Dev-Auth": "dev-bypass",
};

// Load the work-list once, shared across all VUs (memory-efficient for ~100k rows).
// Each non-empty line is "appraisalId,propertyId,propertyType" (no header; see export-worklist.sql).
const workList = new SharedArray("worklist", function () {
  const raw = open(WORKLIST);
  const rows = [];
  for (const line of raw.split("\n")) {
    const t = line.trim();
    if (!t) continue;
    const [appraisalId, propertyId, propertyType] = t.split(",").map((s) => s.trim());
    if (appraisalId && propertyId && propertyType) {
      rows.push({ appraisalId, propertyId, propertyType });
    }
  }
  return rows;
});

const filled = new Counter("details_filled");

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    // Exactly one iteration per work-list row, processed once each.
    fill_detail: {
      executor: "shared-iterations",
      vus: VUS,
      iterations: workList.length,
      maxDuration: "8h",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    "http_req_duration{name:land-detail}": ["p(95)<2000"],
    "http_req_duration{name:land-and-building-detail}": ["p(95)<2000"],
    "http_req_duration{name:condo-detail}": ["p(95)<2000"],
    checks: ["rate>0.99"],
  },
};

// helpers (plain k6 runtime; Math.random allowed here)
function randDecimal(min, max, dp) {
  const v = min + Math.random() * (max - min);
  return Number(v.toFixed(dp));
}
// Thailand bounding box-ish: lat 6–20, lon 97–106.
const randLat = () => randDecimal(6, 20, 6);
const randLon = () => randDecimal(97, 106, 6);

// Coded enum (List<string>) fields default to a single assumed-valid code "01".

// Land portion — shared by land-detail (L) and land-and-building-detail (LB).
// All fields optional except each title's TitleNumber/TitleType.
function buildLandFields() {
  const geo = randomGeo();
  const govPricePerSqWa = 1000 + Math.floor(Math.random() * 200) * 500;
  const rai = Math.floor(Math.random() * 10);
  const ngan = Math.floor(Math.random() * 4);
  const sqWa = Math.floor(Math.random() * 100);
  const totalSqWa = rai * 400 + ngan * 100 + sqWa;

  return {
    propertyName: `Plot ${randDigits(4)}`,
    landDescription: "Load-test land parcel",
    latitude: randLat(),
    longitude: randLon(),
    subDistrict: geo.subDistrict,
    district: geo.district,
    province: geo.province,
    landOffice: `Land Office ${geo.province}`,
    ownerName: randomName(),
    isOwnerVerified: true,
    street: `Road ${randDigits(2)}`,
    soi: `Soi ${randDigits(2)}`,
    distanceFromMainRoad: randDecimal(0, 5, 1),
    village: `Village ${randDigits(2)}`,
    addressLocation: `Zone ${randDigits(3)}`,
    accessRoadWidth: randDecimal(4, 12, 1),
    roadFrontage: randDecimal(10, 40, 1),
    numberOfSidesFacingRoad: 1 + Math.floor(Math.random() * 2),
    hasElectricity: true,
    electricityDistance: randDecimal(0, 200, 0),
    landZoneType: ["01"],
    plotLocationType: ["01"],
    publicUtilityType: ["01"],
    landUseType: ["01"],
    landEntranceExitType: ["01"],
    transportationAccessType: ["01"],
    evictionType: ["01"],
    northAdjacentArea: "Adjacent plot",
    northBoundaryLength: randDecimal(10, 40, 1),
    southAdjacentArea: "Adjacent plot",
    southBoundaryLength: randDecimal(10, 40, 1),
    eastAdjacentArea: "Public road",
    eastBoundaryLength: randDecimal(10, 40, 1),
    westAdjacentArea: "Adjacent plot",
    westBoundaryLength: randDecimal(10, 40, 1),
    remark: "load-test",
    titles: [
      {
        titleNumber: `LT-${randDigits(8)}`,
        titleType: "DEED",
        landParcelNumber: randDigits(4),
        surveyNumber: randDigits(4),
        rai: rai,
        ngan: ngan,
        squareWa: sqWa,
        governmentPricePerSqWa: govPricePerSqWa,
        governmentPrice: totalSqWa * govPricePerSqWa,
        remark: "load-test",
      },
    ],
  };
}

// Building portion — added on top of the land portion for LB.
function buildBuildingFields() {
  const sellingPrice = 1_000_000 + Math.floor(Math.random() * 200) * 50_000;
  return {
    buildingNumber: `B${randDigits(3)}`,
    buildingConditionType: "01",
    isUnderConstruction: false,
    isAppraisable: true,
    buildingType: "01",
    numberOfFloors: 1 + Math.floor(Math.random() * 4),
    buildingMaterialType: "01",
    buildingStyleType: "01",
    isResidential: true,
    buildingAge: Math.floor(Math.random() * 30),
    constructionYear: 1995 + Math.floor(Math.random() * 30),
    structureType: ["01"],
    roofFrameType: ["01"],
    roofType: ["01"],
    ceilingType: ["01"],
    interiorWallType: ["01"],
    exteriorWallType: ["01"],
    fenceType: ["01"],
    utilizationType: "01",
    totalBuildingArea: randDecimal(60, 500, 1),
    sellingPrice: sellingPrice,
    remark: "load-test",
  };
}

// Condo body (U) — its own endpoint/shape.
function buildCondoDetail() {
  const geo = randomGeo();
  const sellingPrice = 1_000_000 + Math.floor(Math.random() * 200) * 50_000;
  return {
    propertyName: `Unit ${randDigits(4)}`,
    condoName: `Condo ${randDigits(3)}`,
    buildingNumber: `A${randDigits(2)}`,
    roomNumber: `${randDigits(3)}`,
    floorNumber: `${1 + Math.floor(Math.random() * 30)}`,
    usableArea: randDecimal(28, 150, 1),
    titleNumber: `CU-${randDigits(8)}`,
    titleType: "DEED",
    builtOnTitleNumber: `LT-${randDigits(8)}`,
    latitude: randLat(),
    longitude: randLon(),
    subDistrict: geo.subDistrict,
    district: geo.district,
    province: geo.province,
    landOffice: `Land Office ${geo.province}`,
    ownerName: randomName(),
    isOwnerVerified: true,
    buildingConditionType: "01",
    street: `Road ${randDigits(2)}`,
    soi: `Soi ${randDigits(2)}`,
    accessRoadWidth: randDecimal(4, 12, 1),
    publicUtilityType: ["01"],
    decorationType: "01",
    buildingAge: Math.floor(Math.random() * 30),
    constructionYear: 1995 + Math.floor(Math.random() * 30),
    numberOfFloors: 8 + Math.floor(Math.random() * 30),
    roomLayoutType: "01",
    locationViewType: ["01"],
    roofType: ["01"],
    totalBuildingArea: randDecimal(28, 150, 1),
    facilityType: ["01"],
    environmentType: ["01"],
    sellingPrice: sellingPrice,
    remark: "load-test",
  };
}

// family -> { path, body() }
const FAMILY = {
  L: { path: "land-detail", body: buildLandFields },
  LB: { path: "land-and-building-detail", body: () => ({ ...buildLandFields(), ...buildBuildingFields() }) },
  U: { path: "condo-detail", body: buildCondoDetail },
};

export default function () {
  const item = workList[exec.scenario.iterationInTest];
  if (!item) return;

  const fam = FAMILY[item.propertyType];
  if (!fam) {
    console.error(`skip ${item.appraisalId}/${item.propertyId}: unknown propertyType '${item.propertyType}'`);
    return;
  }

  const url = `${BASE_URL}/appraisals/${item.appraisalId}/properties/${item.propertyId}/${fam.path}`;
  const res = http.put(url, JSON.stringify(fam.body()), {
    headers: HEADERS,
    tags: { name: fam.path },
  });

  const ok = check(res, { [`${fam.path} 204`]: (r) => r.status === 204 });
  if (ok) {
    filled.add(1);
  } else {
    console.error(`${fam.path} failed (${item.appraisalId}/${item.propertyId}): ${res.status} ${res.body}`);
  }
}
