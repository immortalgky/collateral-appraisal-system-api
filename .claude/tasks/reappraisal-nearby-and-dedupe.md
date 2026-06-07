# Reappraisal: Nearby Query Rewrite + Duplicate Prevention

## Goal
(a) Fix Group Appraisal nearby list to include in-system appraisals (not only COLLATREV candidates).
(b) Prevent duplicate reappraisal requests via three layers.

---

## Tasks

### Part A — Nearby query rewrite

- [x] 1. Extend `NearbyReappraisalCandidate` DTO with `AppraisalId (Guid?)`, `CandidateId (Guid?)`, `Source (string)`. Remove `CollateralId`, `CollateralName` (not in spec). Keep `OldAppraisalReportNumber`, `CustomerName`, `CurrentValue`, `AppraisalDate`, `RemainingDay`, `ReviewType` (nullable), `DistanceKm`, `Latitude`, `Longitude`.

- [x] 2. Rewrite `GetReappraisalCandidateByIdQueryHandler.nearbySql`:
  - UNION of:
    - In-system appraisals from `appraisal.vw_AppraisalList` joined to geo coord CROSS APPLY (clone pattern from `FetchCoordinatesAsync`)
    - Pending candidates from `request.ReappraisalCandidates` with non-null GeoPoint
  - Deduped: when both sides match on AppraisalNumber/SurveyNumber, produce ONE row with both AppraisalId + CandidateId, Source='Candidate'
  - Exclusions: self candidate ID, self appraisal ID (resolved from main SurveyNumber), already-in-flight (NOT EXISTS on non-terminal RequestDetails), Consumed/Deleted candidates
  - AppraisalDate: candidate ReviewDate when present, else `vw_AppraisalList.AppointmentDateTime` (CAST to DATE)
  - RemainingDay = DATEDIFF(DAY, CAST(GETUTCDATE() AS DATE), AppraisalDate)

### Part B — Duplicate prevention

- [x] 3. Layer 3 — `vw_ReappraisalCandidates.sql`: Add NOT EXISTS filter to exclude rows whose appraisal already has a non-terminal reappraisal Request open.

- [x] 4. Extend `InitiateReappraisalCommand` + endpoint body with `NearbyAppraisalIds (List<Guid>)`.

- [x] 5. Extend `InitiateReappraisalResult` with `Skipped (List<SkippedReappraisalItem>)`.

- [x] 6. Rewrite `InitiateReappraisalCommandHandler`:
  - Merge CandidateIds + NearbyAppraisalIds into a working list
  - For NearbyAppraisalIds: look up matching candidate by AppraisalNumber=SurveyNumber (attach if found for Layer 2 mark-Consumed)
  - Layer 1 dedupe: single Dapper IN-query on `request.RequestDetails` for non-terminal requests
  - For candidate items: existing flow (create from candidate data + MarkConsumed)
  - For appraisal-only items: fetch minimal data via Dapper, create Request with Channel="AS400", PrevAppraisalId=appraisalId
  - Return Skipped list

- [x] 7. `dotnet build` — 0 errors

---

## Review

### Changes Made

**Part A — Nearby query rewrite**

1. **`GetReappraisalCandidateByIdResult.cs`** — Extended `NearbyReappraisalCandidate` with `AppraisalId`, `CandidateId`, `Source`; made `ReviewType` nullable (in-system appraisals won't have a ReviewType); removed `CollateralId`/`CollateralName` (not needed per spec). Fixed `CifNumber` to nullable (in-system rows have no CIF number).

2. **`GetReappraisalCandidateByIdQueryHandler.cs`** — Complete rewrite of the nearby query:
   - Two-branch UNION: (a) in-system appraisals from `appraisal.vw_AppraisalList` with coord CROSS APPLY (same pattern as ingestion job), (b) Pending candidates from `request.ReappraisalCandidates` with GeoPoint
   - LEFT JOIN between the two sides on `AppraisalNumber = SurveyNumber` to produce merged rows
   - Source = 'Candidate' when a candidate row exists, else 'InSystem'
   - Exclusions: self by CandidateId + AppraisalId, non-terminal in-flight requests, Consumed/Deleted candidates
   - AppraisalDate from candidate ReviewDate when present, else AppointmentDateTime

**Part B — Duplicate prevention**

3. **`vw_ReappraisalCandidates.sql`** (Layer 3) — Added NOT EXISTS filter that hides candidates whose prior appraisal already has an open (non-terminal) reappraisal Request.

4. **`InitiateReappraisalCommand.cs`** — Added `NearbyAppraisalIds` parameter; guard now accepts 0 CandidateIds when NearbyAppraisalIds is non-empty.

5. **`InitiateReappraisalResult.cs`** — Added `Skipped` list with `SkippedReappraisalItem` record carrying `AppraisalId`, `OldAppraisalReportNumber`, `Reason`.

6. **`InitiateReappraisalCommandHandler.cs`** — Full rewrite:
   - Merges both lists into `WorkingItem (AppraisalId?, Candidate?)` list
   - For NearbyAppraisalIds: attempts to find matching Pending candidate by SurveyNumber
   - Layer 1 dedupe: single Dapper batch query; any resolved AppraisalId already in-flight → Skipped
   - Candidate branch: existing create flow
   - Appraisal-only branch: fetches CustomerName + AppraisalNumber from `appraisal.Appraisals` + `appraisal.vw_AppraisalList`, creates minimal Request

### TODOs left
- TODO(confirm): `AppraisalDate` fallback from `vw_AppraisalList.AppointmentDateTime` — confirm this is the right field
- TODO(confirm): reappraisal purpose code "03"
- TODO(confirm): appraisal-only branch request data (CustomerName from vw_AppraisalList; Address=null)
