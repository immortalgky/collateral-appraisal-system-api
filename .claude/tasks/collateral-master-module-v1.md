# Collateral Master Module — v1 Design (Deep Review)

> **Purpose of this document:** complete design specification for the Collateral master module — schema, ER diagram, tables with column-level detail, domain model, API surface, write path, backfill/replay, admin ops, migration plan, and verification. Detailed enough for an implementation agent to execute without re-discovery, after the live session closes.

---

## 1. Context & Goals

The system performs collateral appraisals. Today, each appraisal carries its own copy of property data with no persistent cross-appraisal record. Three real workflows are unmet:

1. **Reappraisal (3-year revaluation)** — same property, fresh valuation, identity should be remembered.
2. **Construction inspection (Progressive)** — building under construction must be re-inspected; previous % must be visible.
3. **Appeal** — customer disputes 1st-round value; new request to a different company against the same property.

Plus a broader need: **at request creation time**, detect whether the requested collateral was appraised before — for prefill, for audit ("how many times, when, by whom"), and for business-rule enforcement (e.g. exclude prior company on appeal).

The Collateral module solves this by maintaining a **`CollateralMaster`** aggregate keyed by `(TitleNumber, Province)`, deduped from completed appraisals, with an immutable engagement history.

---

## 2. Scope

**In scope (v1) — ships the three workflow cases for all titled/registered collateral types:**
- **4 master types** with type-specific dedup keys:
  - **Land** — `LandOfficeCode + Province + Amphur + Tambon + TitleDeedType + TitleDeedNo + SurveyNo/ParcelNo`
  - **Condo** — `LandOfficeCode + CondoRegistrationNumber + Building + Floor + Unit + TitleNumber + TitleType`
  - **Leasehold** — `LeaseRegistrationNo + UnderlyingMasterId + Lessor + Lessee + LeaseTermStart` (standalone master with reference to underlying Land/Condo)
  - **Machine** — `MachineRegistrationNo` if present, else `SerialNo + Brand + Model + Manufacturer` (two-tier; `LocationOwner` dropped — accepted slight collision risk)
- Construction tracking on `LandDetail` (flag + overall % + last inspection pointer); per-item prefill via Appraisal-side query
- Land master tracks both `LastAppraisedValue` (land only) + `LastTotalAppraisedValue` (land + buildings)
- Identity (locked) + last-known (editable) two-tier prefill model
- Engagement history with metadata + JSON snapshot per appraisal
- Read API: lookup, catalog (paginated, basic filters incl. type, `IsUnderConstructionAtLastAppraisal`), history list, snapshot drill-in
- Backfill of historical completed appraisals + per-appraisal admin replay
- Admin edit / soft-delete / restore
- Validation gate in `Appraisal.Complete()` — per-type identity field requirements
- Drop & rebuild of existing scaffold under `Modules/Collateral/`
- Frontend integration: lookup at request creation, autocomplete, appeal company-exclusion, Progressive prefill (per-item Previous Progress), Reappraisal prefill — for all 4 types

**v1.1 (next iteration) — analytics layer:**
- `GET /collateral-masters/{id}/construction-progress` — time series across engagement snapshots (requires `vw_CollateralConstructionProgress` with `OPENJSON` extraction)
- `GET /collateral-masters/analytics/summary` — aggregate counts by province / date range / construction status
- Frontend: construction-progress chart on the master detail screen, admin analytics dashboard

**Out of scope (no current plan):**
- Vehicle / Vessel masters (same shape as Machine; add later if needed)
- Master merge
- Manual master creation without an appraisal
- Bulk admin operations
- Editing or deleting engagement snapshots (immutable)
- Caching layer
- Deep prefill of pricing methods, market comparables, valuation, photos (each company does fresh — business rule)

---

## 3. Decision Log

| # | Decision | Why |
|---|---|---|
| 1 | Clean redesign | Existing scaffold doesn't reflect current appraisal property model |
| 2 | Land + Condo only | Real estate first; building is improvement on land |
| 3 | `(TitleNumber, Province)` dedup key for both | Condo unit deed (หนังสือกรรมสิทธิ์ห้องชุด) is unique; consistent with land |
| 4 | Two-tier (Identity locked, Last-known editable) | Simple; matches reappraisal prefill use case |
| 5 | Snapshot per engagement (metadata + JSON) | Self-contained immutable history; supports trend analytics |
| 6 | Thin event + query back from Appraisal | Assumes appraisal data immutable after `Completed` |
| 7 | One appraisal → N master upserts (one per property) | Multi-property appraisals are real |
| 8 | Dead-letter when key missing | Forces upstream data quality (validation gate to be added in Appraisal) |
| 9 | Full read suite (lookup + catalog + autocomplete + analytics) | All requested in v1 |
| 10 | One-shot backfill + admin replay endpoint | History needed from day one |
| 11 | Edit + soft-delete only (no merge) | YAGNI on merge; soft-delete + audit log covers correction |
| 12 | Approach C: shared root + 1:1 detail entity | Cleanest schema; easy to add types later |
| 13 | Construction tracking on master = flag + overall % + pointer to last inspection (no per-item denormalization) | Existing `ConstructionInspection` model in Appraisal module already holds granular per-item data; duplicating on master would drift |
| 14 | Per-item prefill (Previous Progress %) done by Appraisal/UI reading prior `ConstructionInspection` directly | Match by `ConstructionWorkItemId` (template FK); fallback to `WorkItemName` for free-text items |
| 15 | Engagement snapshot captures full `ConstructionInspection` (summary or work-detail array) | Drives construction-progress time-series without joining live inspection tables |
| 16 | Detection-at-request derived on read, NOT stored on `AppraisalProperty` | Eliminates cross-module write + consistency risk; lookup by `(TitleNumber, Province)` is cheap with the unique index |
| 17 | Engagement records `AppraisalCompanyId` (+ name denorm) | Required by Request/Quotation flow to exclude prior companies on appeal |
| 18 | Prefill scope is identity + last-known + under-construction only | Pricing/comparables/valuation/photos must be fresh per appraisal — each company does their own analysis |
| 19 | Appeal = `AppraisalType.New` against same title | No new enum value; engagement count naturally accumulates |
| 20 | Trend analytics (construction time-series + analytics summary) deferred to v1.1 | Core write/read/prefill loop ships first; OPENJSON view + admin chart add complexity not blocking the three workflow cases |
| 21 | Land master tracks both `LastAppraisedValue` (land only) and `LastTotalAppraisedValue` (land + buildings) | Land-only is misleading on catalog/lookup screens; total avoids confusion; per-building drill-in stays in engagement snapshot |
| 22 | v1 scope expanded from {Land, Condo} to {Land, Condo, Leasehold, Machine} | Covers all collateral types that have official/business identifiers per Thai practice (image reference) |
| 23 | Dedup keys live on type-specific detail tables, not on `CollateralMasters` | Each type has different keys; keeping dedup on details lets each type have its own appropriate unique index. `CollateralMasters` becomes type-discriminator + audit + soft-delete only |
| 24 | Land key = `LandOfficeCode + Province + Amphur + Tambon + TitleDeedType + TitleDeedNo + SurveyNo/ParcelNo` | Verbatim per Thai Land Office practice. `TitleDeedNo` alone is unique only within a Land Office |
| 25 | Condo key = `LandOfficeCode + CondoRegistrationNumber + Building + Floor + Unit + TitleNumber + TitleType` | New columns `TitleNumber` + `TitleType` added to `CondoAppraisalDetail` (matches Land's pattern; `CondoTitleDeedNo` was a misnomer — the unit deed is captured by TitleNumber + TitleType) |
| 26 | Leasehold = standalone master with reference to underlying | A lease IS its own collateralizable asset distinct from the underlying property; tracked independently |
| 27 | Leasehold key = `ContractNo + UnderlyingMasterId + Lessor + Lessee + LeaseTermStart` | Reuses existing `LeaseAgreementDetail.ContractNo` (no new column); maps to Thai Tor Dor 11 reference |
| 28 | Machine key = `MachineRegistrationNo` if present, else `SerialNo + Brand + Model + Manufacturer` (two-tier, no `LocationOwner`) | New `SerialNo` column added to `MachineryAppraisalDetail`. `LocationOwner` dropped from key per user — accepted slight collision risk for simpler model |
| 29 | Leasehold appraisal auto-creates the underlying master if missing | A leasehold implies the underlying property exists; one appraisal can create both masters |

---

## 4. Assumptions

1. Completed appraisals are immutable (snapshot-via-query is safe).
2. Validation will be added in `Appraisal.Complete()` (`Modules/Appraisal/Appraisal/Domain/Appraisals/Appraisal.cs:660`) so Land/Condo cannot reach `Completed` without title + province. **Concrete plan in Section 16 handoff and Section 13 pre-checks** — this is not just a hand-waved future task.
3. Condo "title number" means the unit deed, not the underlying land title.
4. `Province` is stored as a stable identifier (canonical name or code) — same value coming from Land and Condo paths.
5. Admin role exists in the auth system; reused for admin endpoints.
6. Scale: tens of thousands of masters within 3 years; ~2-3 engagements per master.
7. Schema name: `collateral`.
8. `AppraisalCompletedIntegrationEvent` carries `AppraisalId` (or will be extended to).
9. All entity Ids use `Guid.CreateVersion7()` per project convention; EF configs declare `NEWSEQUENTIALID()` as server fallback.
10. Audit fields (`CreatedOn`, `CreatedBy`, `UpdatedOn`, `UpdatedBy`) populated by the existing audit interceptor.

---

## 5. Schema & Data Model

### 5.0 Schema Overview (at-a-glance)

All tables under schema `collateral`. New for this module — no shared tables.

| # | Table | Role | Cardinality |
|---|---|---|---|
| 5.2 | `CollateralMasters` | Aggregate root (type discriminator + audit + soft-delete; **no dedup columns**) | — |
| 5.3 | `LandDetails` | Type-specific detail when `Type = 'Land'` (incl. dedup key + construction tracking) | 1:1 with master |
| 5.5 | `CondoDetails` | Type-specific detail when `Type = 'Condo'` (incl. dedup key) | 1:1 with master |
| 5.5b | `LeaseholdDetails` | Type-specific detail when `Type = 'Leasehold'` (refs underlying master) | 1:1 with master |
| 5.5c | `MachineDetails` | Type-specific detail when `Type = 'Machine'` (two-tier dedup key) | 1:1 with master |
| 5.6 | `CollateralEngagements` | Immutable history row per appraisal-property pair (with JSON snapshot) | 1:N from master |
| 5.7 | `CollateralMasterAuditLog` | Admin-action audit trail | 1:N from master |
| 5.8 | `CollateralBackfillReport` | One-shot backfill outcome log (no FK) | standalone |

**Dedup model:** each type-specific detail table owns its dedup columns + filtered unique index. The upsert service finds the master via the right detail's unique key for the property's type.

**Cross-module additions:** none — Section 5.9 confirms `appraisal.AppraisalProperties` gets no new column. Cross-schema match is derived at query time using the type-specific dedup key.

**Cross-module pointers (no DB FK):**
- `LandDetails.LastConstructionInspectionId` → `appraisal.ConstructionInspections.Id` (soft pointer; UI handles 404 if dangling)

**Internal cross-aggregate pointer (within `collateral` schema):**
- `LeaseholdDetails.UnderlyingMasterId` → `CollateralMasters.Id` (hard FK; cascade is RESTRICTED — can't delete an underlying that has leaseholds)

### 5.1 ER Diagram

```mermaid
erDiagram
    CollateralMaster {
        uniqueidentifier Id PK
        nvarchar CollateralType "Land|Condo|Leasehold|Machine"
        nvarchar OwnerName
        bit IsDeleted
        datetime2 CreatedOn
        nvarchar CreatedBy
        datetime2 UpdatedOn
        nvarchar UpdatedBy
    }
    LandDetail {
        uniqueidentifier CollateralMasterId PK_FK
        nvarchar LandOfficeCode "dedup"
        nvarchar Province "dedup"
        nvarchar Amphur "dedup"
        nvarchar Tambon "dedup"
        nvarchar TitleDeedType "dedup"
        nvarchar TitleDeedNo "dedup"
        nvarchar SurveyOrParcelNo "dedup"
        nvarchar Street
        nvarchar Village
        nvarchar PostalCode
        decimal Latitude
        decimal Longitude
        nvarchar LandShapeType
        nvarchar LandZoneType
        nvarchar UrbanPlanningType
        decimal AccessRoadWidth
        decimal RoadFrontage
        decimal LandArea
        bit IsUnderConstructionAtLastAppraisal
        decimal OverallConstructionProgressPercent
        uniqueidentifier LastConstructionInspectionId
        uniqueidentifier LastAppraisalId
        nvarchar LastAppraisalNumber
        datetime2 LastAppraisedDate
        decimal LastAppraisedValue "land only"
        decimal LastTotalAppraisedValue "land + buildings"
    }
    CondoDetail {
        uniqueidentifier CollateralMasterId PK_FK
        nvarchar LandOfficeCode "dedup"
        nvarchar CondoRegistrationNumber "dedup"
        nvarchar BuildingNumber "dedup"
        nvarchar FloorNumber "dedup"
        nvarchar UnitNumber "dedup"
        nvarchar TitleNumber "dedup"
        nvarchar TitleType "dedup"
        nvarchar CondoName
        nvarchar Province
        decimal UsableArea
        nvarchar LocationType
        int BuildingAge
        int ConstructionYear
        nvarchar ModelName
        uniqueidentifier LastAppraisalId
        nvarchar LastAppraisalNumber
        datetime2 LastAppraisedDate
        decimal LastAppraisedValue
    }
    LeaseholdDetail {
        uniqueidentifier CollateralMasterId PK_FK
        nvarchar LeaseRegistrationNo "dedup (Tor Dor 11)"
        uniqueidentifier UnderlyingMasterId "dedup, FK to CollateralMaster"
        nvarchar Lessor "dedup"
        nvarchar Lessee "dedup"
        date LeaseTermStart "dedup"
        date LeaseTermEnd
        int LeaseTermMonths
        decimal AnnualRent
        nvarchar LeasePurpose
        uniqueidentifier LastAppraisalId
        nvarchar LastAppraisalNumber
        datetime2 LastAppraisedDate
        decimal LastAppraisedValue
    }
    MachineDetail {
        uniqueidentifier CollateralMasterId PK_FK
        nvarchar MachineRegistrationNo "dedup tier-1, nullable"
        nvarchar SerialNo "dedup tier-2"
        nvarchar Brand "dedup tier-2"
        nvarchar Model "dedup tier-2"
        nvarchar Manufacturer "dedup tier-2"
        int YearOfManufacture
        nvarchar MachineCondition
        decimal MachineAge
        nvarchar EngineNo
        nvarchar ChassisNo
        uniqueidentifier LastAppraisalId
        nvarchar LastAppraisalNumber
        datetime2 LastAppraisedDate
        decimal LastAppraisedValue
    }
    CollateralEngagement {
        uniqueidentifier Id PK
        uniqueidentifier CollateralMasterId FK
        uniqueidentifier AppraisalId
        nvarchar AppraisalNumber
        uniqueidentifier RequestId
        nvarchar RequestNumber
        uniqueidentifier PropertyId
        nvarchar AppraisalType
        datetime2 AppraisalDate
        decimal AppraisedValue
        nvarchar AppraiserUserId
        uniqueidentifier AppraisalCompanyId
        nvarchar AppraisalCompanyName
        nvarchar Snapshot "JSON"
        datetime2 CreatedOn
    }
    CollateralMasterAuditLog {
        uniqueidentifier Id PK
        uniqueidentifier CollateralMasterId FK
        nvarchar Action
        nvarchar ChangedFields "JSON"
        nvarchar Reason
        nvarchar ChangedBy
        datetime2 ChangedAt
    }
    CollateralBackfillReport {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId
        nvarchar Status
        nvarchar Message
        datetime2 RunAt
    }
    CollateralMaster ||--o| LandDetail               : "1:1 when Type=Land"
    CollateralMaster ||--o| CondoDetail              : "1:1 when Type=Condo"
    CollateralMaster ||--o| LeaseholdDetail          : "1:1 when Type=Leasehold"
    CollateralMaster ||--o| MachineDetail            : "1:1 when Type=Machine"
    LeaseholdDetail }o--|| CollateralMaster          : "UnderlyingMasterId — FK (RESTRICT delete)"
    CollateralMaster ||--o{ CollateralEngagement     : "1:N history (immutable snapshots)"
    CollateralMaster ||--o{ CollateralMasterAuditLog : "1:N admin actions"
    CollateralEngagement }o--|| Appraisal            : "AppraisalId (cross-module ref)"
    CollateralEngagement }o--|| AppraisalProperty    : "PropertyId (cross-module ref, idempotency)"
    LandDetail       }o..o| ConstructionInspection   : "LastConstructionInspectionId (cross-module pointer, no FK)"
    CollateralMaster }o..o{ AppraisalProperty        : "match at query time on type-specific dedup key — NO stored FK"
    CollateralBackfillReport }o..|| Appraisal        : "AppraisalId (no FK)"
```

---

### 5.2 Table: `collateral.CollateralMasters`

Aggregate root. Type discriminator + audit + soft-delete. **No dedup columns** — those live on type-specific detail tables (5.3 / 5.5 / 5.5b / 5.5c).

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `Id` | uniqueidentifier | NO | `NEWSEQUENTIALID()` | PK; populated client-side via `Guid.CreateVersion7()` in domain `Create()` |
| `CollateralType` | nvarchar(20) | NO | — | Enum stored as string: `Land` \| `Condo` \| `Leasehold` \| `Machine` |
| `OwnerName` | nvarchar(200) | YES | — | Identity (admin-editable). For Leasehold this is the **Lessee** (the party who holds the leasehold interest) |
| `IsDeleted` | bit | NO | 0 | Soft delete |
| `CreatedOn` | datetime2(7) | NO | — | Audit interceptor |
| `CreatedBy` | nvarchar(100) | YES | — | Audit interceptor |
| `UpdatedOn` | datetime2(7) | YES | — | Audit interceptor |
| `UpdatedBy` | nvarchar(100) | YES | — | Audit interceptor |

**Indexes**
- PK clustered: `Id`
- Nonclustered: `(CollateralType)` — supports catalog filtering by type
- Nonclustered: `(IsDeleted)` — supports list queries

---

### 5.3 Table: `collateral.LandDetails`

1:1 with `CollateralMasters` where `CollateralType = 'Land'`. Owns the Land dedup key.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `CollateralMasterId` | uniqueidentifier | NO | — | PK + FK → `CollateralMasters.Id` (cascade delete) |
| **Dedup key** | | | | |
| `LandOfficeCode` | nvarchar(20) | NO | — | Land Office (สำนักงานที่ดิน) code; required for true uniqueness |
| `Province` | nvarchar(100) | NO | — | จังหวัด (admin level 1) |
| `Amphur` | nvarchar(100) | NO | — | อำเภอ/เขต (admin level 2) |
| `Tambon` | nvarchar(100) | NO | — | ตำบล/แขวง (admin level 3) |
| `TitleDeedType` | nvarchar(20) | NO | — | e.g. `Chanote` (โฉนด), `NorSor3` (น.ส.3), `NorSor3Kor` (น.ส.3ก), `SorKor1` (ส.ค.1) |
| `TitleDeedNo` | nvarchar(50) | NO | — | Deed number |
| `SurveyOrParcelNo` | nvarchar(50) | YES | — | Survey/parcel number (เลขที่ดิน/ระวาง); part of the key when present |
| **Address (owned)** | | | | |
| `Street` | nvarchar(200) | YES | — | |
| `Village` | nvarchar(200) | YES | — | |
| `PostalCode` | nvarchar(20) | YES | — | |
| `Latitude` | decimal(9,6) | YES | — | Coordinate (owned) |
| `Longitude` | decimal(9,6) | YES | — | |
| **Last-known land context** | | | | |
| `LandShapeType` | nvarchar(50) | YES | — | Last-known |
| `LandZoneType` | nvarchar(50) | YES | — | Last-known |
| `UrbanPlanningType` | nvarchar(50) | YES | — | Last-known |
| `AccessRoadWidth` | decimal(10,2) | YES | — | Meters |
| `RoadFrontage` | decimal(10,2) | YES | — | Meters |
| `LandArea` | decimal(18,4) | YES | — | Square meters |
| `IsUnderConstructionAtLastAppraisal` | bit | NO | 0 | Computed by upsert service from prior `ConstructionInspection`. **Snapshot in time at the last appraisal — not live state.** A property at 50% in 2025 with no new appraisal still shows `1` in 2026. UI labels and analytics text must reflect this nuance. Drives catalog filter "active construction at last appraisal". |
| `OverallConstructionProgressPercent` | decimal(7,4) | YES | — | Rolled up from prior inspection. Summary mode → use `SummaryCurrentProgressPct`. Full-detail mode → weighted by `ProportionPct`. NULL when there's no inspection. |
| `LastConstructionInspectionId` | uniqueidentifier | YES | — | Cross-module pointer to `appraisal.ConstructionInspections.Id` from the last appraisal. Used by UI to fetch per-item Previous Progress for prefill. |
| `LastAppraisalId` | uniqueidentifier | YES | — | Last-known pointer |
| `LastAppraisalNumber` | nvarchar(50) | YES | — | Denormalized |
| `LastAppraisedDate` | datetime2(7) | YES | — | |
| `LastAppraisedValue` | decimal(18,2) | YES | — | **Land only.** Value of the bare land at last appraisal. |
| `LastTotalAppraisedValue` | decimal(18,2) | YES | — | **Land + buildings.** Sum of `land.AppraisedValue` and all `building.AppraisedValue` where `BuildingAppraisalDetail.BuiltOnTitleNumber == land.TitleNumber` from the same appraisal. Use this on catalog/lookup screens. |

**Indexes**
- PK clustered: `CollateralMasterId`
- **Filtered unique nonclustered:** `(LandOfficeCode, Province, Amphur, Tambon, TitleDeedType, TitleDeedNo, SurveyOrParcelNo) WHERE master.IsDeleted = 0` — enforces dedup. Implemented as an indexed view or via trigger if filtering on a column from the parent table is needed; alternative is to denormalize `IsDeleted` onto `LandDetails` for indexability.
- Nonclustered: `(LandOfficeCode, TitleDeedNo)` — supports partial-key lookup
- Filtered nonclustered: `(IsUnderConstructionAtLastAppraisal) WHERE IsUnderConstructionAtLastAppraisal = 1` — supports analytics summary

**Known limitation:** if a building is appraised in a separate appraisal that contains no Land property, no land master is updated, so that building's value is not reflected in `LastTotalAppraisedValue` until the next combined appraisal. Rare in practice; not solved in v1.

---

### 5.4 Construction tracking — no separate table

Earlier drafts proposed a `LandUnderConstructionBuildings` collection table. **Removed.** Rationale:

- The Appraisal module already owns `ConstructionInspection` (1:1 with `AppraisalProperty`) with full-detail `ConstructionWorkDetail` rows that carry `PreviousProgressPct`, `CurrentProgressPct`, `ProportionPct`, `ConstructionValue`, etc. Per-item progress is sourced from the Parameter module's `ConstructionWorkGroup` / `ConstructionWorkItem` template.
- Collateral master should **point** at the prior inspection rather than denormalize its rows. This avoids drift, eliminates building match-key complexity, and keeps the master schema lean.
- Per-item match across visits uses `ConstructionWorkItemId` (template FK) on `ConstructionWorkDetail` — no `BuildingNumber + ModelName` heuristic needed.
- Engagement snapshots (Section 5.6) capture the full inspection JSON per visit — drives the construction-progress time-series endpoint without joining live tables.

What remains on `LandDetail` for construction (Section 5.3 above): `IsUnderConstructionAtLastAppraisal`, `OverallConstructionProgressPercent`, `LastConstructionInspectionId`.

---

### 5.5 Table: `collateral.CondoDetails`

1:1 with `CollateralMasters` where `CollateralType = 'Condo'`. Owns the Condo dedup key.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `CollateralMasterId` | uniqueidentifier | NO | — | PK + FK → `CollateralMasters.Id` (cascade delete) |
| **Dedup key** | | | | |
| `LandOfficeCode` | nvarchar(200) | NO | — | Land Office issuing the condo unit deed (sourced from existing dropdown — `AdministrativeAddress.LandOffice`) |
| `CondoRegistrationNumber` | nvarchar(200) | NO | — | Project / condo registration code |
| `BuildingNumber` | nvarchar(50) | NO | — | Building within the project |
| `FloorNumber` | nvarchar(50) | NO | — | |
| `UnitNumber` | nvarchar(50) | NO | — | Room/unit number (sourced from `RoomNumber`) |
| `TitleNumber` | nvarchar(50) | NO | — | Unit deed number (new column on `CondoAppraisalDetail`) |
| `TitleType` | nvarchar(20) | NO | — | Unit deed type (new column on `CondoAppraisalDetail`) |
| **Identity-extra** | | | | |
| `CondoName` | nvarchar(200) | YES | — | Display name |
| `Province` | nvarchar(100) | YES | — | For UI grouping |
| **Last-known** | | | | |
| `UsableArea` | decimal(18,4) | YES | — | Square meters |
| `LocationType` | nvarchar(50) | YES | — | |
| `BuildingAge` | int | YES | — | Years |
| `ConstructionYear` | int | YES | — | |
| `ModelName` | nvarchar(200) | YES | — | |
| `LastAppraisalId` | uniqueidentifier | YES | — | |
| `LastAppraisalNumber` | nvarchar(50) | YES | — | |
| `LastAppraisedDate` | datetime2(7) | YES | — | |
| `LastAppraisedValue` | decimal(18,2) | YES | — | |

**Indexes**
- PK clustered: `CollateralMasterId`
- **Filtered unique nonclustered:** `(LandOfficeCode, CondoRegistrationNumber, BuildingNumber, FloorNumber, UnitNumber, TitleNumber, TitleType) WHERE IsDeleted = 0` — IsDeleted denormalized onto detail (option A)
- Nonclustered: `(LandOfficeCode, TitleNumber, TitleType)` — supports partial-key lookup

---

### 5.5b Table: `collateral.LeaseholdDetails`

1:1 with `CollateralMasters` where `CollateralType = 'Leasehold'`. The lease IS its own collateralizable asset distinct from the underlying property.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `CollateralMasterId` | uniqueidentifier | NO | — | PK + FK → `CollateralMasters.Id` (cascade delete) |
| **Dedup key** | | | | |
| `LeaseRegistrationNo` | nvarchar(50) | NO | — | Tor Dor 11 reference (ท.ด.11) — official lease registration number |
| `UnderlyingMasterId` | uniqueidentifier | NO | — | FK → `CollateralMasters.Id` (the underlying Land or Condo). **ON DELETE RESTRICT** — cannot delete underlying that has leaseholds |
| `Lessor` | nvarchar(200) | NO | — | Owner who leases out (the "landlord") |
| `Lessee` | nvarchar(200) | NO | — | Lease holder (also stored as `CollateralMasters.OwnerName`) |
| `LeaseTermStart` | date | NO | — | |
| **Last-known** | | | | |
| `LeaseTermEnd` | date | YES | — | |
| `LeaseTermMonths` | int | YES | — | Total term length |
| `AnnualRent` | decimal(18,2) | YES | — | Latest known annual rent |
| `LeasePurpose` | nvarchar(200) | YES | — | Residential / commercial / industrial / etc. |
| `LastAppraisalId` | uniqueidentifier | YES | — | |
| `LastAppraisalNumber` | nvarchar(50) | YES | — | |
| `LastAppraisedDate` | datetime2(7) | YES | — | |
| `LastAppraisedValue` | decimal(18,2) | YES | — | |

**Indexes**
- PK clustered: `CollateralMasterId`
- **Filtered unique nonclustered:** `(LeaseRegistrationNo, UnderlyingMasterId, Lessor, Lessee, LeaseTermStart) WHERE master.IsDeleted = 0`
- Nonclustered: `(UnderlyingMasterId)` — supports "all leaseholds over this property" lookups
- Nonclustered: `(LeaseRegistrationNo)` — partial-key lookup

> **Auto-create rule:** when a leasehold appraisal arrives, the upsert service first ensures the underlying Land/Condo master exists (creating it from the same appraisal's underlying-property data if necessary), then creates/updates the leasehold master with `UnderlyingMasterId` set.

---

### 5.5c Table: `collateral.MachineDetails`

1:1 with `CollateralMasters` where `CollateralType = 'Machine'`. Two-tier dedup: registration if available, composite otherwise.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `CollateralMasterId` | uniqueidentifier | NO | — | PK + FK → `CollateralMasters.Id` (cascade delete) |
| **Dedup key — tier 1 (preferred)** | | | | |
| `MachineRegistrationNo` | nvarchar(50) | YES | — | Machinery registrar number (จดทะเบียนเครื่องจักร). When present, sole dedup key. |
| **Dedup key — tier 2 (fallback when tier 1 missing)** | | | | |
| `SerialNo` | nvarchar(100) | YES | — | Required when `MachineRegistrationNo` is NULL |
| `Brand` | nvarchar(100) | YES | — | Required when `MachineRegistrationNo` is NULL |
| `Model` | nvarchar(100) | YES | — | Required when `MachineRegistrationNo` is NULL |
| `Manufacturer` | nvarchar(200) | YES | — | Required when `MachineRegistrationNo` is NULL |
| **Identity-extra & last-known** | | | | |
| `EngineNo` | nvarchar(100) | YES | — | |
| `ChassisNo` | nvarchar(100) | YES | — | |
| `YearOfManufacture` | int | YES | — | |
| `MachineCondition` | nvarchar(50) | YES | — | |
| `MachineAge` | decimal(5,2) | YES | — | Years |
| `LastAppraisalId` | uniqueidentifier | YES | — | |
| `LastAppraisalNumber` | nvarchar(50) | YES | — | |
| `LastAppraisedDate` | datetime2(7) | YES | — | |
| `LastAppraisedValue` | decimal(18,2) | YES | — | |

**Indexes**
- PK clustered: `CollateralMasterId`
- **Filtered unique nonclustered (tier 1):** `(MachineRegistrationNo) WHERE MachineRegistrationNo IS NOT NULL AND IsDeleted = 0`
- **Filtered unique nonclustered (tier 2):** `(SerialNo, Brand, Model, Manufacturer) WHERE MachineRegistrationNo IS NULL AND IsDeleted = 0`
- Nonclustered: `(SerialNo)` — partial-key lookup

> **Upsert precedence:**
> 1. If incoming `MachineRegistrationNo` is non-empty, look up by it. Match → update. No match → check composite for collision (e.g. previously stored as composite, now registered) → if found, "promote" by setting registration number + erasing composite-only-dedup status. Otherwise create.
> 2. If incoming `MachineRegistrationNo` is empty, look up by composite `(SerialNo, Brand, Model, Manufacturer)`. Match → update. Otherwise create.
> 3. Validation gate: if both registration **and** composite are missing → reject (dead-letter).

> **Promotion edge case:** when an existing composite-keyed master later gets a registration number, the upsert "promotes" it (preserves Id and engagements; updates dedup tier). Document this behavior in tests.

---

### 5.6 Table: `collateral.CollateralEngagements`

History rows. One per appraisal-property pair.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `Id` | uniqueidentifier | NO | `NEWSEQUENTIALID()` | PK; `Guid.CreateVersion7()` |
| `CollateralMasterId` | uniqueidentifier | NO | — | FK → `CollateralMasters.Id` (cascade delete) |
| `AppraisalId` | uniqueidentifier | NO | — | Source appraisal |
| `AppraisalNumber` | nvarchar(50) | NO | — | Denormalized |
| `RequestId` | uniqueidentifier | NO | — | Source request |
| `RequestNumber` | nvarchar(50) | NO | — | Denormalized |
| `PropertyId` | uniqueidentifier | NO | — | Source `AppraisalProperty.Id` |
| `AppraisalType` | nvarchar(20) | NO | — | `New` \| `ReAppraisal` \| `Progressive` \| `PreAppraisal` |
| `AppraisalDate` | datetime2(7) | NO | — | When appraisal completed (or report date) |
| `AppraisedValue` | decimal(18,2) | YES | — | Final value |
| `AppraiserUserId` | nvarchar(100) | YES | — | Individual appraiser |
| `AppraisalCompanyId` | uniqueidentifier | YES | — | For appeal-flow company exclusion |
| `AppraisalCompanyName` | nvarchar(200) | YES | — | Denormalized |
| `Snapshot` | nvarchar(max) | NO | — | JSON of property fields as appraised |
| `CreatedOn` | datetime2(7) | NO | — | Audit interceptor |

**Indexes**
- PK clustered: `Id`
- **Unique** nonclustered: `(AppraisalId, PropertyId)` — idempotency
- Nonclustered: `(CollateralMasterId, AppraisalDate DESC)` — supports history pagination
- Nonclustered: `(AppraisalCompanyId)` — supports "exclude prior company" lookup

**Snapshot JSON structure** (example for Land with full-detail construction inspection):
```json
{
  "type": "Land",
  "titleNumber": "12345",
  "province": "Bangkok",
  "ownerName": "...",
  "address": { "street": "...", "village": "...", "district": "...", "subDistrict": "...", "postalCode": "..." },
  "coordinates": { "latitude": 13.756, "longitude": 100.501 },
  "landShapeType": "...",
  "landZoneType": "...",
  "urbanPlanningType": "...",
  "accessRoadWidth": 6.0,
  "roadFrontage": 12.5,
  "landArea": 400.0,
  "appraisedValue": 5000000.00,
  "totalAppraisedValue": 16500000.00,
  "buildingsOnLand": [
    { "appraisalPropertyId": "...", "buildingNumber": "B1", "modelName": "Main House", "totalBuildingArea": 240.0, "appraisedValue": 10000000.00 },
    { "appraisalPropertyId": "...", "buildingNumber": "B2", "modelName": "Garage", "totalBuildingArea": 40.0, "appraisedValue": 1000000.00 },
    { "appraisalPropertyId": "...", "buildingNumber": "B3", "modelName": "Storage", "totalBuildingArea": 20.0, "appraisedValue": 500000.00 }
  ],
  "constructionInspection": {
    "inspectionId": "...",
    "isFullDetail": true,
    "totalValue": 19856000.00,
    "overallCurrentProgressPercent": 93.05,
    "remark": "...",
    "workDetails": [
      {
        "workDetailId": "...",
        "constructionWorkGroupId": "...",
        "groupCode": "BuildingStructure",
        "constructionWorkItemId": "...",
        "itemCode": "Pillar",
        "workItemName": "Pillar",
        "displayOrder": 1,
        "proportionPct": 12.50,
        "previousProgressPct": 50.00,
        "currentProgressPct": 100.00,
        "currentProportionPct": 12.50,
        "constructionValue": 2482000.00,
        "previousPropertyValue": 1241000.00,
        "currentPropertyValue": 2482000.00
      }
      /* ...more work details */
    ]
  }
}
```

For summary-mode inspection, `constructionInspection.isFullDetail = false` and `workDetails` is omitted; instead the snapshot contains `summaryDetail`, `summaryPreviousProgressPct`, `summaryPreviousValue`, `summaryCurrentProgressPct`, `summaryCurrentValue`. When the property has no inspection at all, `constructionInspection` is omitted.

**Snapshot shape per type:**
- **Land** — example above (identity dedup fields + last-known + buildings + construction inspection)
- **Condo** — `{ type: "Condo", landOfficeCode, condoRegistrationOrProject, building, floor, unit, condoTitleDeedNo, condoName, ownerName, usableArea, locationType, ..., appraisedValue }`
- **Leasehold** — `{ type: "Leasehold", leaseRegistrationNo, underlyingMasterId, underlyingType, underlyingDedupKey, lessor, lessee, leaseTermStart, leaseTermEnd, annualRent, leasePurpose, appraisedValue }`
- **Machine** — `{ type: "Machine", machineRegistrationNo?, serialNo, brand, model, manufacturer, locationOwner, engineNo, chassisNo, yearOfManufacture, machineCondition, machineAge, appraisedValue }`

---

### 5.7 Table: `collateral.CollateralMasterAuditLog`

Admin-action audit trail.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `Id` | uniqueidentifier | NO | `NEWSEQUENTIALID()` | PK |
| `CollateralMasterId` | uniqueidentifier | NO | — | FK → `CollateralMasters.Id` |
| `Action` | nvarchar(50) | NO | — | `Edit` \| `SoftDelete` \| `Restore` |
| `ChangedFields` | nvarchar(max) | YES | — | JSON `{ "field": { "from": x, "to": y } }` |
| `Reason` | nvarchar(500) | NO | — | Required from admin |
| `ChangedBy` | nvarchar(100) | NO | — | User id |
| `ChangedAt` | datetime2(7) | NO | — | |

**Indexes**
- PK clustered: `Id`
- Nonclustered: `(CollateralMasterId, ChangedAt DESC)`

---

### 5.8 Table: `collateral.CollateralBackfillReport`

One-shot backfill outcome log.

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `Id` | uniqueidentifier | NO | `NEWSEQUENTIALID()` | PK |
| `AppraisalId` | uniqueidentifier | NO | — | Source appraisal |
| `Status` | nvarchar(30) | NO | — | `Processed` \| `SkippedMissingKey` \| `Error` |
| `Message` | nvarchar(1000) | YES | — | Skip reason or exception |
| `RunAt` | datetime2(7) | NO | — | |

**Indexes**
- PK clustered: `Id`
- Nonclustered: `(Status, RunAt DESC)` — admin filter

---

### 5.9 Cross-module: no schema change to `appraisal.AppraisalProperties`

Earlier drafts proposed adding `LinkedCollateralMasterId` on `AppraisalProperty`. **Removed.** Rationale:

- The link can be **derived at query time** by joining `AppraisalProperty` to `CollateralMasters` on `(TitleNumber, Province)` + `Type`. The unique filtered index on the master makes this a single-row lookup.
- Storing the FK introduces cross-module write consistency risk (Collateral commits, then Appraisal-side write fails → stale link). Deriving eliminates this entirely.
- For "is this request a reappraisal?" / "show all reappraisal requests" queries, a SQL view in the read layer (e.g. `vw_AppraisalPropertyMasterMatch`) projects the join. Materialize later if performance demands.

This means the Collateral consumer no longer publishes a cross-module `CollateralMasterLinkedNotification` — one less moving part.

---

### 5.10 Enums & Value Lists

**`CollateralType`** (stored as nvarchar)
- `Land`
- `Condo`
- `Leasehold`
- `Machine`

**`AppraisalType`** (existing — defined in `Appraisal.Domain.Appraisals.AppraisalTypes`)
- `New` (used for first appraisal AND appeal)
- `ReAppraisal` (3-year revaluation)
- `Progressive` (construction inspection)
- `PreAppraisal` (pre-loan estimation)

**`AuditLogAction`** (stored as nvarchar)
- `Edit`
- `SoftDelete`
- `Restore`

**`BackfillStatus`** (stored as nvarchar)
- `Processed`
- `SkippedMissingKey`
- `Error`

---

## 6. Domain Model

### 6.1 Aggregates

**`CollateralMaster`** (aggregate root, in `Modules/Collateral/Collateral/CollateralMasters/Models/`)
- Owns: `LandDetail` OR `CondoDetail` (1:1, mutually exclusive by `CollateralType`)
- Owns: `CollateralEngagement` collection
- Owns: `CollateralMasterAuditLog` collection (cross-cutting; can also be a separate read-only repo)
- Factory methods: `CreateLand(...)`, `CreateCondo(...)` — return master + matching detail
- Behaviors:
  - `UpsertFromLandAppraisal(snapshotData)` — overwrites identity-extra + last-known; updates construction tracking (`IsUnderConstructionAtLastAppraisal`, `OverallConstructionProgressPercent`, `LastConstructionInspectionId`); appends engagement
  - `UpsertFromCondoAppraisal(snapshotData)` — overwrites identity-extra + last-known + appends engagement
  - `EditIdentity(...)`, `EditLastKnown(...)` — admin path; raises domain event with diff for audit log
  - `SoftDelete(reason, by)`, `Restore(reason, by)`

### 6.2 Value Objects / Owned Types

**`Address`** (owned by `LandDetail`)
- `Street`, `Village`, `District`, `SubDistrict`, `PostalCode`

**`Coordinates`** (owned by `LandDetail`)
- `Latitude`, `Longitude`

**`AppraisalSummary`** (owned, embedded into both details)
- `LastAppraisalId`, `LastAppraisalNumber`, `LastAppraisedDate`, `LastAppraisedValue`

### 6.3 Domain Events

Dispatched via existing `DispatchDomainEventInterceptor` (transactional, same DbContext).

- `CollateralMasterCreatedEvent(Id, CollateralType, TitleNumber, Province)`
- `CollateralEngagementAddedEvent(MasterId, EngagementId, AppraisalId)`
- `ConstructionStatusChangedEvent(MasterId, WasUnderConstruction, IsUnderConstruction, FromPercent, ToPercent)` — fires when `IsUnderConstructionAtLastAppraisal` flips or progress crosses meaningful thresholds (useful for downstream notifications, e.g. bank schedules next inspection when construction completes)
- `CollateralMasterEditedEvent(MasterId, ChangedFields, Reason, By)` — handled by `AuditLogWriter` (writes audit row)
- `CollateralMasterSoftDeletedEvent(MasterId, Reason, By)`
- `CollateralMasterRestoredEvent(MasterId, Reason, By)`

Plus a cross-module **MediatR notification**:
- `CollateralMasterLinkedNotification(AppraisalPropertyId, CollateralMasterId)` — published from the Collateral consumer; handled in Appraisal module to set `AppraisalProperty.LinkedCollateralMasterId`.

### 6.4 Repositories & Services

| Type | Responsibility |
|---|---|
| `ICollateralMasterRepository` | EF-backed write + simple lookups by id and dedup key |
| `ICollateralMasterUpsertService` | The single entry point used by consumer / backfill / replay. Calls `IAppraisalQueryService` for source data, performs upsert + engagement append, handles cross-module notification. |
| `IAppraisalQueryService` (in Appraisal module — new) | Exposes `GetAppraisalForCollateralQuery` to return appraisal metadata + filtered Land/Condo properties + nested under-construction buildings |
| `ICollateralMasterReader` | Dapper-backed read model for endpoints. Uses `ISqlConnectionFactory` + `DapperPaginationExtensions.QueryPaginatedAsync<T>`. |

---

## 6.5 Cross-Module Contracts (be explicit)

This module is intentionally connected to the Appraisal module by these contracts. None are decoupled — be explicit so reviewers don't miss them:

| Direction | Contract | Type |
|---|---|---|
| Collateral → Appraisal (write path query) | `IAppraisalQueryService.GetAppraisalForCollateralQuery(appraisalId)` returns metadata + **all properties** (Land + Condo + Building — Building included so the consumer can sum building values onto the matching land master via `BuildingAppraisalDetail.BuiltOnTitleNumber`) + nested `ConstructionInspection` + `OverallCurrentProgressPercent` | In-process MediatR query |
| Appraisal → Collateral (event) | `AppraisalCompletedIntegrationEvent` (with `AppraisalId`) | MassTransit / RabbitMQ |
| Frontend → Appraisal (read for prefill) | `GET /appraisal/construction-inspections/{id}/work-details` | HTTP |
| Cross-schema pointer (no DB FK) | `LandDetail.LastConstructionInspectionId` → `appraisal.ConstructionInspections.Id` | Soft pointer; if inspection is hard-deleted, pointer dangles — UI must handle 404 |
| Cross-schema match (no stored FK) | `(AppraisalProperty.TitleNumber, AppraisalProperty.Address.Province)` ↔ `(CollateralMasters.TitleNumber, CollateralMasters.Province)` | Derived at query time |

**No outbox needed for the consumer** since the consumer only writes to its own (Collateral) DbContext. Cross-module writes were eliminated when `LinkedCollateralMasterId` was dropped.

---

## 7. Prefill Scope Boundary

What Collateral DOES carry / expose:
- Identity (locked): per-type dedup key (Land Office code + admin levels + deed type/no for Land; project + building + floor + unit + deed for Condo; lease registration + parties + term + underlying for Leasehold; registration no OR composite for Machine), plus owner/lessee
- Last-known (editable): address, coordinates, land shape, zoning, areas, condo unit attributes, lease term/rent, machine condition/age — type-appropriate
- Land only — construction status flag + overall % + pointer (`LastConstructionInspectionId`) so the UI can fetch per-item Previous Progress from the prior inspection
- Land only — `LastTotalAppraisedValue` (land + buildings)

What Collateral DOES NOT carry / expose:
- Pricing methods used in prior appraisals
- Market comparables
- Valuation calculations / approaches
- Photos or supporting documents
- Appraisal company internal notes

Each appraisal company performs their own fresh pricing analysis. There is **no deep-clone-from-previous-appraisal feature** in either module.

---

## 8. Three Workflow Cases

### Case 1: Appeal (2nd opinion from a different company)

1. User creates new Request. Most fields copied from original (as the Request module's existing copy feature handles); appointment date + fee + invited companies differ.
2. When the user enters `TitleNumber + Province`, UI calls `GET /collateral-masters/lookup` → returns master + last engagement + companies that have engaged (from `CollateralEngagement.AppraisalCompanyId`).
3. Request flow excludes those prior companies from the invitable list.
4. New appraisal created with `AppraisalType = New`. Identity locked, last-known prefilled. No stored cross-module link — match is implicit via `(TitleNumber, Province)`.
5. On completion: consumer matches `(TitleNumber, Province)` → same master → engagement #2 appended; identity-extra + last-known overwritten.
6. For analytics ("show all reappraisal-style requests"), the read layer joins `AppraisalProperty` to `CollateralMasters` on title+province at query time.

### Case 2: Construction inspection (Progressive)

1. New Request created against the same property.
2. Lookup hits master with `IsUnderConstructionAtLastAppraisal = 1`, `OverallConstructionProgressPercent = X`, `LastConstructionInspectionId = Y`.
3. New appraisal created with `AppraisalType = Progressive`. UI:
   - Prefills identity + last-known from master.
   - Calls Appraisal module: `GET /appraisal/construction-inspections/{Y}/work-details` (or equivalent query) → returns the prior `ConstructionWorkDetail` rows.
   - Seeds the new `ConstructionInspection.WorkDetails`: each new row's `PreviousProgressPct = prior.CurrentProgressPct`, copying `ConstructionWorkItemId`, `ConstructionWorkGroupId`, `WorkItemName`, `ProportionPct`, `ConstructionValue`, `DisplayOrder`.
   - Appraiser updates `CurrentProgressPct` per item; `ProportionPct` and template links stay unless the appraiser changes them.
4. On completion: consumer recomputes `IsUnderConstructionAtLastAppraisal`, `OverallConstructionProgressPercent`, `LastConstructionInspectionId` from the new inspection. If overall progress reached 100%, `IsUnderConstructionAtLastAppraisal = 0` and `ConstructionStatusChangedEvent` fires.
5. Engagement #N appended with full snapshot of the new inspection (preserves the journey for time-series).

### Case 3: Reappraisal (3-year)

1. New Request created.
2. Lookup hits master.
3. New appraisal created with `AppraisalType = ReAppraisal`. UI prefills identity (locked) + last-known (editable, all flagged for re-verification due to age).
4. Last appraised value shown only as read-only reference label (avoids anchoring bias).
5. Pricing analysis, comparables, photos, valuation: appraiser builds fresh.
6. On completion: identical write path as other cases.

> All three cases use the **same** lookup endpoint and the **same** consumer write path. No case-specific code branches — the design is uniform.
>
> **Type-specific behavior in each case:**
> - **Land/Condo/Machine:** all three workflows apply directly. Reappraisal of a Machine = next periodic valuation; Appeal = different appraiser; Progressive doesn't typically apply (machines aren't "under construction").
> - **Leasehold:** Reappraisal and Appeal apply. Progressive doesn't apply (leases aren't built). When a leasehold reappraisal arrives, the underlying Land/Condo master also gets refreshed automatically (engagement #N+1 added to both masters).

---

## 9. Write Path: Consumer Flow

**Trigger:** `AppraisalCompletedIntegrationEvent` (extend with `AppraisalId` if not present).

**Consumer:** `AppraisalCompletedConsumer` (MassTransit) in Collateral module.

**Service entry point:** `ICollateralMasterUpsertService.ProcessAppraisalAsync(Guid appraisalId)`.

```text
ProcessAppraisalAsync(appraisalId):
  appraisal = await mediator.Send(new GetAppraisalForCollateralQuery(appraisalId))
  if appraisal == null: throw NotFound      // dead-letter

  inScopeProperties = appraisal.Properties.Where(p => p.Type in [Land, Condo, Leasehold, Machine])
  buildingProperties = appraisal.Properties.Where(p => p.Type == Building)   // for value summation onto Land masters

  // Validation gate (per-type)
  foreach p in inScopeProperties:
    switch p.Type:
      case Land:
        require LandOfficeCode, Province, Amphur, Tambon, TitleDeedType, TitleDeedNo
      case Condo:
        require LandOfficeCode, CondoRegistrationNumber, BuildingNumber, FloorNumber, UnitNumber (RoomNumber), TitleNumber, TitleType
      case Leasehold:
        require ContractNo, Lessor, Lessee, LeaseTermStart, AND a resolvable underlying property in the same appraisal
      case Machine:
        require RegistrationNo OR (SerialNo AND Brand AND Model AND Manufacturer)
    on missing → throw MissingIdentityKeyException(p.Id)   // dead-letter (or backfill-report row)

  using transaction:
    // Pass 1: Land + Condo + Machine (don't depend on other masters)
    foreach p in inScopeProperties.Where(p => p.Type in [Land, Condo, Machine]):
      master = await repo.FindByDedupKey(p.Type, p.DedupKeyFields)
      if master == null:
        master = CollateralMaster.CreateForType(p.Type, p)
        repo.Add(master)
      else:
        master.UpsertFromAppraisal(p)   // type-specific overwrite of identity-extra + last-known

    // Pass 2: Leasehold (depends on underlying master existing)
    foreach p in inScopeProperties.Where(p => p.Type == Leasehold):
      underlyingMaster = await repo.FindByDedupKey(p.UnderlyingProperty.Type, p.UnderlyingProperty.DedupKeyFields)
      if underlyingMaster == null:
        // auto-create from the underlying property data on the same appraisal
        underlyingMaster = CollateralMaster.CreateForType(p.UnderlyingProperty.Type, p.UnderlyingProperty)
        repo.Add(underlyingMaster)
        // also write engagement for the underlying master (separate from leasehold engagement)
      master = await repo.FindLeaseholdByDedupKey(p.LeaseRegistrationNo, underlyingMaster.Id, p.Lessor, p.Lessee, p.LeaseTermStart)
      if master == null:
        master = CollateralMaster.CreateLeasehold(p, underlyingMaster.Id)
        repo.Add(master)
      else:
        master.UpsertFromAppraisal(p)

      // For Land: recompute construction tracking from p.ConstructionInspection
      //   - IsUnderConstructionAtLastAppraisal = inspection != null && overallProgress < 100
      //   - OverallConstructionProgressPercent = inspection.IsFullDetail
      //       ? Sum(WorkDetails: ProportionPct * CurrentProgressPct / 100)
      //       : SummaryCurrentProgressPct
      //   - LastConstructionInspectionId = inspection?.Id
      //   - Raise ConstructionStatusChangedEvent if flag flips

      // For Land: compute LastTotalAppraisedValue
      //   buildingsForThisLand = buildingProperties.Where(b =>
      //       b.BuildingDetail.BuiltOnTitleNumber == p.TitleNumber)
      //   master.LandDetail.LastAppraisedValue = p.AppraisedValue           // land only
      //   master.LandDetail.LastTotalAppraisedValue =
      //       p.AppraisedValue + buildingsForThisLand.Sum(b => b.AppraisedValue ?? 0)

      // append engagement (idempotent via unique (AppraisalId, PropertyId))
      master.AppendEngagement(
        appraisalId, appraisal.Number, appraisal.RequestId, appraisal.RequestNumber,
        p.Id, appraisal.Type, appraisal.CompletedAt, p.AppraisedValue,
        appraisal.AppraiserUserId, appraisal.CompanyId, appraisal.CompanyName,
        snapshot: BuildSnapshotJson(p)   // includes full ConstructionInspection
      )

    await unitOfWork.SaveChangesAsync()       // domain events fire here, audit + soft-delete events handled
```

**Failure modes**
- `GetAppraisalForCollateralQuery` returns null → dead-letter.
- Validation gate trips → dead-letter; admin replay path covers recovery once data is fixed.
- Two consumers race the same `(AppraisalId, PropertyId)` → unique index throws on second insert; transaction retries with the load-existing branch.

---

## 10. Read API Surface

All under `/collateral-masters`. Carter modules. Dapper read-side.

| # | Method | Path | Auth | Scope | Purpose |
|---|---|---|---|---|---|
| 1 | GET | `/collateral-masters/lookup?type={Land\|Condo\|Leasehold\|Machine}&...typeSpecificDedupParams` | authenticated | **v1** | Live autocomplete + prefill. **Required `type` param** drives which dedup query params are accepted: Land = `landOfficeCode, province, amphur, tambon, titleDeedType, titleDeedNo, surveyOrParcelNo`; Condo = `landOfficeCode, condoRegistrationOrProject, building, floor, unit, condoTitleDeedNo`; Leasehold = `leaseRegistrationNo, underlyingMasterId, lessor, lessee, leaseTermStart`; Machine = `machineRegistrationNo` OR `serialNo+brand+model+manufacturer+locationOwner`. Returns master + detail (Land detail incl. construction tracking + total value) + last engagement summary + list of prior `AppraisalCompanyId`s. |
| 2 | GET | `/collateral-masters?type=&province=&owner=&isUnderConstruction=&minAppraisals=&lastAppraisedFrom=&lastAppraisedTo=&page=&pageSize=&sort=` | admin | **v1** | Paginated catalog browse |
| 3 | GET | `/collateral-masters/{id}` | authenticated | **v1** | Full detail + attached detail entity |
| 4 | GET | `/collateral-masters/{id}/engagements?page=&pageSize=` | authenticated | **v1** | Paginated history desc-by-date |
| 5 | GET | `/collateral-masters/{id}/engagements/{engagementId}` | authenticated | **v1** | Snapshot drill-in |
| 6 | GET | `/collateral-masters/{id}/construction-progress` | authenticated | **v1.1** | Time series across engagement snapshots. Returns per-engagement: `(appraisalDate, appraisalNumber, isFullDetail, overallProgressPct, workDetails: [{ groupCode, itemCode, proportionPct, currentProgressPct }])`. Backed by SQL view `vw_CollateralConstructionProgress` extracting JSON from `CollateralEngagements.Snapshot`. |
| 7 | GET | `/collateral-masters/analytics/summary?province=&from=&to=` | admin | **v1.1** | Aggregates: total masters, masters appraised in range, masters with `IsUnderConstructionAtLastAppraisal = 1` |
| 8 | PATCH | `/collateral-masters/{id}` | admin | **v1** | Edit identity / last-known. Body requires `Reason`. |
| 9 | DELETE | `/collateral-masters/{id}` | admin | **v1** | Soft delete. Body requires `Reason`. |
| 10 | POST | `/collateral-masters/{id}/restore` | admin | **v1** | Restore soft-deleted master. Body requires `Reason`. |
| 11 | POST | `/collateral-masters/admin/backfill` | admin | **v1** | Kick off one-shot backfill (returns job id) |
| 12 | GET | `/collateral-masters/admin/backfill-report?status=&page=&pageSize=` | admin | **v1** | Paginated backfill outcomes |
| 13 | POST | `/collateral-masters/admin/replay/{appraisalId}` | admin | **v1** | Re-run upsert for a single appraisal |

**SQL views in `Database/Scripts/Views/Collateral/`**
- **v1:** `vw_CollateralMasters` — master + detail + engagement aggregates (`EngagementCount`, `LastAppraisedDate`, `LastAppraisedValue`, `IsUnderConstructionAtLastAppraisal`, `OverallConstructionProgressPercent`)
- **v1:** `vw_CollateralEngagements` — engagements joined with master metadata for fast history pagination
- **v1.1:** `vw_CollateralConstructionProgress` — flattened time series across all engagement snapshots; uses `OPENJSON` (or `JSON_VALUE`/`JSON_QUERY`) on `CollateralEngagements.Snapshot` to extract `constructionInspection.workDetails` array per engagement

---

## 11. Backfill + Replay

**Hosted job:** `CollateralBackfillJob` registered as `IHostedService`-equivalent (kicked off via admin endpoint, not on app start).

```text
RunBackfillAsync():
  cursor = first
  while more appraisals:
    batch = appraisalQuery.GetCompleted(cursor, pageSize: 100)
    foreach appraisal in batch (oldest CompletedAt first):
      try:
        await upsertService.ProcessAppraisalAsync(appraisal.Id)
        WriteReport(Processed)
      catch MissingIdentityKeyException ex:
        WriteReport(SkippedMissingKey, ex.Message)
      catch Exception ex:
        WriteReport(Error, ex.ToString())
    cursor = batch.last
```

- Idempotent: unique index on `(AppraisalId, PropertyId)` blocks duplicate engagements; master upsert is by dedup key. Safe to re-run.
- Replay endpoint calls the same `ProcessAppraisalAsync(appraisalId)` for one appraisal — used after upstream data is corrected.

---

## 12. Admin Operations

| Operation | Endpoint | Audit log entry |
|---|---|---|
| Edit identity (TitleNumber/Province/OwnerName) or last-known | `PATCH /collateral-masters/{id}` | `Edit` with field diff |
| Soft-delete | `DELETE /collateral-masters/{id}` | `SoftDelete` |
| Restore | `POST /collateral-masters/{id}/restore` | `Restore` |

> Construction tracking has no admin endpoints. Construction data is owned by the Appraisal module's `ConstructionInspection`; corrections go through the source appraisal. The Collateral master's construction fields (`IsUnderConstructionAtLastAppraisal`, `OverallConstructionProgressPercent`, `LastConstructionInspectionId`) are recomputed by the upsert service whenever the underlying appraisal is replayed.

**Constraints:**
- Identity edit rejected with 409 if the new `(TitleNumber, Province)` collides with another non-deleted master.
- All admin endpoints require a `Reason` string in the body (validated non-empty).
- Audit log writes happen via domain event handlers — handlers run within the same SaveChanges (transactional, same DbContext).

---

## 13. Migration of Existing Scaffold

### Pre-checks (read-only, before writing any migration)

1. `SELECT COUNT(*) FROM collateral.CollateralMasters` (and other existing tables) — confirm no production data, or surface to user.
2. **Inbound FK check.** Before dropping old tables, verify nothing else references them:
   ```sql
   SELECT
     OBJECT_SCHEMA_NAME(parent_object_id) + '.' + OBJECT_NAME(parent_object_id) AS ReferencingTable,
     OBJECT_SCHEMA_NAME(referenced_object_id) + '.' + OBJECT_NAME(referenced_object_id) AS ReferencedTable
   FROM sys.foreign_keys
   WHERE OBJECT_SCHEMA_NAME(referenced_object_id) = 'collateral';
   ```
   Any inbound references must be dropped or refactored before the migration runs.
3. Solution-wide search for old types outside `Modules/Collateral/`:
   - `CollateralMaster`, `ICollateralRepository`, `ICollateralService`, `CollateralLand`, `CollateralBuilding`, `CollateralCondo`, `CollateralMachine`, `CollateralVehicle`, `CollateralVessel`, `LandTitle`, `CollateralEngagement`
4. Search for HTTP routes under `/collateral` — coordinate with consumers if any external clients exist.
5. **Validation gate impact check.** Query for already-`Completed` appraisals that would now fail the new validation:
   ```sql
   -- pseudocode — adapt to actual schema
   SELECT a.Id, a.AppraisalNumber
   FROM appraisal.Appraisals a
   JOIN appraisal.AppraisalProperties p ON p.AppraisalId = a.Id
   WHERE a.Status = 'Completed'
     AND p.PropertyType IN ('Land','Condo')
     AND (
        /* land: no titles or all titles empty */
        /* condo: empty unit deed */
        /* either: empty Address.Province */
     );
   ```
   These appear in the `BackfillReport` as `SkippedMissingKey` — surface the count to the user before running backfill, and decide: fix in source, or accept skip.

### Migration

1. Delete `Modules/Collateral/Collateral/Migrations/` and existing entity classes / configurations / repository / service.
2. Rewrite `CollateralDbContext.cs`, `CollateralModule.cs`, entities, configurations per Section 5.
3. Run: `dotnet ef migrations add InitialCollateralMaster --project Modules/Collateral/Collateral --startup-project Bootstrapper/Api`
4. The migration's `Up()` should drop old tables explicitly (or `MigrationBuilder.Sql("DROP TABLE IF EXISTS collateral.X")`) before creating new schema.
5. Add a small migration in the **Appraisal** module to add `AppraisalProperty.LinkedCollateralMasterId` (nullable Guid).
6. `CollateralModule.cs` re-registers:
   - `ICollateralMasterRepository` → `CollateralMasterRepository`
   - `ICollateralMasterUpsertService` → `CollateralMasterUpsertService`
   - `ICollateralMasterReader` → `CollateralMasterReader` (Dapper)
   - MassTransit consumer `AppraisalCompletedConsumer`
   - Carter endpoints (auto-registered via `collateralAssembly` already in `Program.cs`)
   - MediatR handlers (auto-registered)
7. `Program.cs` does **not** change — `AddCollateralModule(configuration)` and `UseCollateralModule()` retain their signatures.
8. Run migration locally → verify schema → run backfill in staging → validate counts → prod.

> No backwards-compat shims. Per project rule: avoid compatibility hacks.

---

## 14. Verification / Test Plan

### Build & migration

- [ ] `dotnet build` clean
- [ ] `dotnet ef migrations add InitialCollateralMaster ...` produces expected up/down
- [ ] `dotnet ef database update` against fresh local DB succeeds
- [ ] Appraisal-side migration adds `LinkedCollateralMasterId` column

### Write path (integration tests)

- [ ] **Test 1 — first appraisal creates master.** Seed completed Appraisal with one Land property (title + province) and a full-detail `ConstructionInspection` (3 work details, overall 50%). Trigger `ProcessAppraisalAsync`. Assert: 1 `CollateralMasters`, 1 `LandDetails` with `IsUnderConstructionAtLastAppraisal=1` + `OverallConstructionProgressPercent≈50` + `LastConstructionInspectionId` set, 1 `CollateralEngagements` row whose JSON snapshot contains the inspection with all 3 work details.
- [ ] **Test 2 — idempotency.** Re-run same input. No duplicate rows; engagement count unchanged.
- [ ] **Test 3 — Progressive updates.** Seed second appraisal (`Type=Progressive`) same title with full-detail inspection at higher overall %. Assert: same master id, engagement count = 2, `OverallConstructionProgressPercent` updated, `LastConstructionInspectionId` repointed to the new inspection.
- [ ] **Test 4 — construction completion.** Third appraisal where overall progress reaches 100%. Assert: `IsUnderConstructionAtLastAppraisal = 0`; engagements still record full history; `ConstructionStatusChangedEvent` raised.
- [ ] **Test 5 — summary-mode inspection.** Inspection in summary mode (no work details). Assert: `OverallConstructionProgressPercent` = `SummaryCurrentProgressPct`; snapshot JSON contains summary fields and no `workDetails` array.
- [ ] **Test 6 — appeal.** Two appraisals against same title with `Type=New` from different companies. Assert: 1 master, 2 engagements, both `AppraisalCompanyId` values present.
- [ ] **Test 7 — multi-property.** One appraisal with 2 land properties + 1 condo property. Assert: 3 masters created (or upserted), 3 engagements.
- [ ] **Test 8 — derived match.** Query joining `AppraisalProperty` to `CollateralMasters` on `(TitleNumber, Province)` returns the master for an in-flight (not-yet-completed) appraisal — proves the read-time link works.
- [ ] **Test 9 — missing key.** Land property with empty title. Assert: dead-letter / `MissingIdentityKeyException` thrown; no rows written.
- [ ] **Test 10 — validation gate.** Attempt to call `Appraisal.Complete()` with a Land property whose `Titles` collection is empty (or whose only title has empty `TitleNumber`). Assert: throws domain validation exception; status remains `UnderReview`.
- [ ] **Test 11 — building value rollup.** Seed appraisal with Land (฿5M, title=12345) + 3 Building properties whose `BuiltOnTitleNumber=12345` (฿10M, ฿1M, ฿0.5M). Trigger `ProcessAppraisalAsync`. Assert: `LandDetail.LastAppraisedValue = 5M`, `LandDetail.LastTotalAppraisedValue = 16.5M`, snapshot `buildingsOnLand` array contains all 3.
- [ ] **Test 12 — building filter by title.** Seed appraisal with 2 Land properties (titles A and B) + 2 Buildings (one `BuiltOnTitleNumber=A`, one `BuiltOnTitleNumber=B`). Assert: each land master gets its own building summed correctly; no cross-contamination.
- [ ] **Test 13 — Condo upsert.** Seed appraisal with one Condo property with full dedup key. Assert: 1 Condo master + 1 CondoDetail row, dedup unique index hit on second seed.
- [ ] **Test 14 — Leasehold auto-create underlying.** Seed appraisal with one Leasehold property + one Land property the leasehold references (via title). No prior masters exist. Assert: 2 masters created (Land first, Leasehold second), Leasehold's `UnderlyingMasterId` points at the Land master, 2 engagement rows.
- [ ] **Test 15 — Leasehold over existing underlying.** Underlying Land master already exists. New appraisal with Leasehold property only. Assert: 1 new Leasehold master (no second Land created), `UnderlyingMasterId` correctly resolved.
- [ ] **Test 16 — Machine tier-1 dedup.** Two appraisals with same `MachineRegistrationNo`. Assert: 1 master, 2 engagements.
- [ ] **Test 17 — Machine tier-2 dedup.** Two appraisals with empty `MachineRegistrationNo` but same `(SerialNo, Brand, Model, Manufacturer)`. Assert: 1 master, 2 engagements.
- [ ] **Test 18 — Machine promotion.** Appraisal #1 with composite-only machine (no registration). Appraisal #2 same composite + a registration number. Assert: same master Id, registration set, both engagements present (proves promotion preserves identity).
- [ ] **Test 19 — Leasehold underlying RESTRICT delete.** Try to soft-delete a Land master that has an active Leasehold. Assert: rejected with clear error.

### Read path

- [x] `GET /collateral-masters/lookup` returns the matched master with active under-construction list and prior company ids
- [x] `GET /collateral-masters/{id}` returns full master + type detail + underlying master summary (Leasehold)
- [x] `GET /collateral-masters/{id}/engagements` returns history desc-by-date, paginated
- [x] `GET /collateral-masters/{id}/engagements/{engagementId}` returns full engagement with Snapshot JSON
- [x] `GET /collateral-masters` (catalog, admin) filters by type/province/owner/isUnderConstruction/minAppraisals/lastAppraisedFrom-to; sort whitelist
- [x] SQL views: `vw_CollateralMasters`, `vw_CollateralEngagements` in `Database/Scripts/Views/Collateral/`
- [x] Integration tests: Lookup hit/miss, GetById shape, engagement pagination, snapshot drill-in, catalog endpoint reachable
- [ ] **(v1.1)** `GET /collateral-masters/{id}/construction-progress` returns time series across snapshots
- [ ] **(v1.1)** `GET /collateral-masters/analytics/summary` returns expected counts

### Admin path

- [ ] PATCH master with valid changes → updates + audit log row written
- [ ] PATCH master with colliding `(TitleNumber, Province)` → 409 Conflict
- [ ] PATCH without `Reason` → 400
- [ ] DELETE master → `IsDeleted=1`; lookup no longer returns it; engagements still queryable by id
- [ ] POST restore → cleared; lookup returns again
### Backfill / replay

- [ ] Backfill against a DB with N completed appraisals creates expected masters + engagements
- [ ] BackfillReport rows written for each appraisal with correct status
- [ ] Re-running backfill is idempotent
- [ ] Replay endpoint with a previously-skipped appraisal id (after data fix) succeeds

### Frontend smoke (manual)

- [ ] Request creation — entering known title triggers autocomplete with prior history banner
- [ ] Appeal — invitable companies list excludes prior `AppraisalCompanyId`s
- [ ] Progressive appraisal — when prior `LastConstructionInspectionId` exists, UI fetches prior `ConstructionWorkDetail` rows and seeds new inspection with `PreviousProgressPct = prior CurrentProgressPct` per work item, matched by `ConstructionWorkItemId` (template FK), free-text fallback by `WorkItemName`
- [ ] Progressive appraisal — summary-mode prior inspection prefills new inspection's summary `PreviousProgressPct` with prior `SummaryCurrentProgressPct`
- [ ] Reappraisal — identity prefilled (locked), last-known prefilled (editable), last value shown read-only, no pricing/comparables carried over

---

## 15. Out of Scope

- Master merge (deferred to v2)
- Manual master creation without an appraisal
- Machinery / Vehicle / Vessel masters
- Editing or deleting engagement snapshots (immutable)
- Caching layer
- Cross-region / multi-tenant concerns
- Deep prefill of pricing methods, comparables, valuation, photos (business rule)

---

## 16. Implementation Handoff Notes

### Repos involved (both must ship together for v1)

| Repo | Local path | What lives here |
|---|---|---|
| **Backend** | `~/Developer/collateral-appraisal-system-api/` | New `Modules/Collateral/` rewrite (aggregate, EF, consumer, endpoints, SQL views, migration). Appraisal-side changes (validation gate in `Appraisal.Complete()`, new `OverallCurrentProgressPercent` on `ConstructionInspection`, query for prior inspection). |
| **Frontend** | `~/Developer/collateral-appraisal-system-app/` | Lookup integration in request creation flow; appeal company-exclusion; per-type prefill UI for Land/Condo/Leasehold/Machine; Progressive prefill (per-work-item Previous Progress fetch); admin screens (catalog, master detail, history, edit/soft-delete/restore, backfill report). Type-aware UI components for the 4 collateral types. |

The two repos are coordinated by:
- Backend exposes the read API surface in Section 10
- Frontend consumes both Collateral lookup endpoints and the new Appraisal-side `GET /appraisal/construction-inspections/{id}/work-details`

### Agent split

Per `feedback_implementation_agents.md` — split implementation:

1. **Backend → ddd-expert** — drives:
   - New aggregate, value objects, domain events
   - DbContext, EF configurations, migration
   - Upsert service (consumer / backfill / replay all share it) — including construction-status recompute logic (overall % rollup from `ConstructionInspection` summary or full-detail)
   - MassTransit consumer + cross-module MediatR notification
   - Carter endpoints (read + admin + backfill/replay)
   - SQL views in `Database/Scripts/Views/Collateral/` — `vw_CollateralConstructionProgress` uses `OPENJSON` against `Snapshot`
   - Drop existing scaffold; rewrite `CollateralModule.cs`
   - Appraisal-side:
     - `IAppraisalQueryService.GetAppraisalForCollateralQuery` returning **all** property data (Land + Condo + Building so the consumer can sum building values onto the matching land master) + nested `ConstructionInspection` (summary or full-detail with work details) + `OverallCurrentProgressPercent`
     - **New computed property** on `ConstructionInspection`: `OverallCurrentProgressPercent` — full-detail = `Sum(WorkDetails.CurrentProportionPct)`; summary = `SummaryCurrentProgressPct ?? 0`. Reused by Collateral upsert service (don't recompute).
     - **New query endpoint:** `GET /appraisal/construction-inspections/{inspectionId}/work-details` returning prior work details for prefill (or expose via existing inspection read API)
     - **Validation gate in `Appraisal.Complete()`** at `Modules/Appraisal/Appraisal/Domain/Appraisals/Appraisal.cs` (~line 660). Add before `UpdateStatus(AppraisalStatus.Completed)`:
       - **Land:** `Titles` collection must contain ≥1 title with non-empty `TitleDeedNo` + `TitleDeedType`; `LandOfficeCode`, `Province`, `Amphur`, `Tambon` must be non-empty.
       - **Condo:** `LandOfficeCode` (`Address.LandOffice`), `CondoRegistrationNumber`, `BuildingNumber`, `FloorNumber`, `RoomNumber`, `TitleNumber`, `TitleType` must all be non-empty.
       - **Leasehold:** `ContractNo`, `LessorName`, `LesseeName`, `LeaseStartDate` non-empty AND the appraisal must contain an underlying Land/Condo property the leasehold references.
       - **Machine:** `RegistrationNo` non-empty OR all of (`SerialNo`, `Brand`, `Model`, `Manufacturer`) non-empty.
       - Use existing `ValidateStatus()` pattern; throw domain exception with clear message identifying the offending property + which fields are missing.
     - **No `LinkedCollateralMasterId` column.** Match is derived at read time.

2. **Frontend → react-expert** — drives (in `~/Developer/collateral-appraisal-system-app/`):
   - Lookup integration in request creation flow (type-aware autocomplete; type selector drives which dedup fields the user enters; "previously appraised" banner)
   - Per-type identity entry components: Land form (LandOfficeCode + admin levels + deed type/no), Condo form (project + building + floor + unit + deed), Leasehold form (lease no + parties + term + underlying picker), Machine form (registration no OR composite)
   - Appeal flow: company exclusion fed by prior `AppraisalCompanyId`s from lookup
   - Progressive appraisal start:
     - Prefill identity (locked) + last-known (editable) from Collateral lookup
     - When `LastConstructionInspectionId` is present, fetch prior inspection from Appraisal module and seed new `ConstructionInspection.WorkDetails`: `PreviousProgressPct = prior.CurrentProgressPct`, copy `ConstructionWorkItemId`/`ConstructionWorkGroupId`/`WorkItemName`/`ProportionPct`/`ConstructionValue`/`DisplayOrder` (matched by `ConstructionWorkItemId`, free-text fallback by `WorkItemName`)
     - Summary-mode prior inspection: copy `SummaryCurrentProgressPct` → new `SummaryPreviousProgressPct`
   - Reappraisal start: prefill identity (locked) + last-known (editable, with re-verify hint); read-only prior-value reference
   - Admin screens (v1): catalog, master detail, engagement history, edit/soft-delete/restore, backfill report
   - Admin screens (v1.1): construction-progress chart on master detail, analytics summary dashboard

3. **Final review → reviewer agent** — audit before merge:
   - Aggregate boundaries clean, no leakage between modules
   - Domain events transactional via `DispatchDomainEventInterceptor`
   - All admin actions audit-logged
   - Idempotency proven (unique index + tests)
   - Soft-link rewrite path correct (cross-module notification)
   - All Dapper params use `DynamicParameters` (per memory), `DateOnly` converted with `.ToDateTime()` (per memory)
   - SQL views handle soft-delete filter correctly
   - No hardcoded role gates in UI (per memory)

### Sequencing recommendation

**v1:**
1. Schema + migration + entity classes (no behavior yet) — get tables in place
2. Appraisal-side: add `OverallCurrentProgressPercent` to `ConstructionInspection`; validation gate in `Appraisal.Complete()`; query for prior `ConstructionInspection`
3. Upsert service + consumer + idempotency tests (incl. construction-status rollup)
4. Read endpoints + Dapper readers + SQL views (`vw_CollateralMasters`, `vw_CollateralEngagements`)
5. Admin endpoints + audit log handlers
6. Backfill + replay + report endpoint
7. Frontend integration in this order: lookup at request creation → progressive prefill (incl. per-work-item prior progress fetch) → reappraisal prefill → appeal company-exclusion → admin screens (catalog, edit, history)

**v1.1 (after v1 is stable in prod):**
8. `vw_CollateralConstructionProgress` SQL view (`OPENJSON`-based)
9. `/construction-progress` + `/analytics/summary` endpoints
10. Frontend: construction-progress chart + analytics dashboard

Each step should land with tests; reviewer agent runs at the end of v1, again at the end of v1.1.
