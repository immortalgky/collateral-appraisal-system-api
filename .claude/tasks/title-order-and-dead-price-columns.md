# Title order + property form schema + dead price columns

Branch `feat/appraisal/data-correction-documents` in both repos (continued after #524 / #452 merged).
Status: **built, uncommitted — waiting for the user's test.** Approved 2026-09-30: one PR per repo after testing;
"first title" changes in AS400/regulatory/LOS accepted; condo `TotalBuildingArea` kept (pricing reads it).

## Findings this plan rests on

1. **Property pages submit zod output.** A key the schema does not declare is dropped and the update
   overwrites the whole record. The one key that mattered: `titles[].id` — every save deleted and
   re-created all titles. (Measured on 60 dev records: toPayload(form) vs toPayload(schema.parse(form)).)
2. **Title order is stored nowhere.** `request.RequestTitles` (Id = Guid v7) and `appraisal.LandTitles`
   (Id = NEWSEQUENTIALID, inserted by EF in random temp-key order) both come back in Id order.
   SQL Server sorts v7 by its random tail. Integration probe: 12 of 12 runs shuffled (8 titles).
3. **`SellingPrice` / `ForcedSalePrice` on `BuildingAppraisalDetails` and `CondoAppraisalDetails` are dead.**
   No page has an input; the only writers are the property Create/Update endpoints echoing the payload and the
   copy-from-previous-appraisal path; the only readers are the property GETs (form + 360 view). The PMA
   prices live on `appraisal.AppraisalProperties` and are what LOS / reports read. Dev: 63 building rows
   non-null (59 are 0), 45 condo rows (43 are 0).

## Part A — schema keeps the title id (FE) — written, uncommitted

- [x] `appraisal/schemas/form.ts`: `landTitleItem.id`
- [x] `schemas/form.carriedKeys.test.ts` (fails without the fix)
- [ ] Trim the carried keys to what Part C leaves: drop `sellingPrice` / `forcedSalePrice`; keep or drop
      `encroachmentArea`, building `buildingInsurancePrice`, condo `totalBuildingArea` per the scope question below
- [ ] Manual: open a multi-title land, Save without edits → title ids and order unchanged

## Part B — store the title order (BE + FE)

Column `SequenceNumber int NOT NULL DEFAULT 0` on `request.RequestTitles` and `appraisal.LandTitles`.
Readers sort `SequenceNumber, Id`, so rows that predate the column (all 0) keep today's order — no backfill.

Request module
- [ ] `RequestTitle.SequenceNumber`; set from list index in `CreateRequestService.CreateTitlesAsync` and
      `RequestSyncService.SyncTitlesAsync` (incoming order wins, including for updated rows)
- [ ] EF config + migration `AddSequenceNumberToRequestTitles`
- [ ] Order by it in `RequestTitleRepository.GetByRequestIdAsync` / `GetByRequestIdWithDocumentsAsync`,
      `GetRequestByIdQueryHandler`, `ReappraisalInitiatedIntegrationEventHandler`, and the Dapper readers
      (quotation handlers, `AppraisalRepository`, the two Reporting quotation providers) — inventory first

Appraisal module
- [ ] `LandTitle.SequenceNumber`; set in `LandDetailSync.SyncTitles`, `LandPmaApplier`, the four
      Create*Land* handlers and `AppraisalCreationService.AddLandTitleFromRequest`
- [ ] `LandAppraisalDetail.Titles` returns the rows ordered (EF loads an owned collection in key order)
- [ ] EF config + migration `AddSequenceNumberToLandTitles`
- [ ] SQL readers, ~15 files: appraisal summary provider, external book, LOS appraisal result,
      appraisal brief, decision summary, history search, `vw_CollateralResultExport`, `vw_RegulatoryExport`
      + `sp_RegulatoryExport`, `vw_MisCasReport`, `vw_PropertyGroupDetail`, pricing property data, AS400 builders.
      Each "first title" pick changes which title it returns once rows have real sequence numbers — list them
      for the user before editing the AS400 / regulatory ones (output files are an external contract)
- [ ] Same treatment for `ProjectLandTitle` only if it has the same shape (check, do not assume)

Tests
- [ ] Integration: 8 titles in → same 8 out, after create and after a save that adds rows
- [ ] Integration: request submitted with titles A,B,C → land detail holds A,B,C
- [ ] Unit: sync keeps the incoming order when rows are updated in place

FE
- [ ] Nothing expected: the form already sends `titles` as an ordered array. Confirm the table does not re-sort.

## Part C — remove the dead price columns (BE + FE)

Scope as asked: `SellingPrice`, `ForcedSalePrice` on `BuildingAppraisalDetails` and `CondoAppraisalDetails`.
`AppraisalProperties.SellingPrice/ForcedSalePrice/BuildingInsurancePrice` and every PMA endpoint stay.

BE
- [ ] Pre-check on UAT / prod before the drop (user runs):
      `SELECT COUNT(*), SUM(CASE WHEN SellingPrice > 0 OR ForcedSalePrice > 0 THEN 1 ELSE 0 END) FROM appraisal.BuildingAppraisalDetails WHERE SellingPrice IS NOT NULL OR ForcedSalePrice IS NOT NULL` (same for condo)
- [ ] Domain: properties, `Update(...)` parameters and the copy in `BuildingAppraisalDetail`, `CondoAppraisalDetail`
- [ ] Commands + requests + handlers/appliers of Create/Update for B, LB, U, LSB, LS, LSU (12 endpoints)
- [ ] GET result/response/handler for the same six (condo exposes it as `ForceSellingPrice`)
- [ ] EF config (2 files) + migration dropping 4 columns (Down re-adds them nullable, data not restored)
- [ ] `UpdatePropertyHandlerCharacterisationTests` (asserts `SellingPrice: 900m`)
- [ ] Grep Reporting / Integration / Collateral / Database/Scripts once more for an alias reading the detail
      columns — a narrow grep already missed the 360 view once

FE
- [ ] `mappers.ts`: building, condo, land-and-building response→form (not the PMA mapper)
- [ ] `form.ts` defaults + the carried keys from Part A
- [ ] 360 view `propertyDetailFieldConfigs.ts`: the two rows in the building and condo blocks (+ unused i18n keys)
- [ ] Regenerate `shared/schemas/v1.ts` after the API change
- [ ] Leave `shared/forms/type*.ts` (unused legacy) alone

Compatibility: the API ignores unknown JSON keys, so an old FE build still sending the two keys keeps working.

## Open questions

1. Same-class dead columns — include in Part C or leave? `BuildingAppraisalDetails.BuildingInsurancePrice`
   (domain comment: "legacy, no reader uses"), `LandAppraisalDetails.EncroachmentArea` (no input, 1 positive row
   in dev), `CondoAppraisalDetails.TotalBuildingArea` (no input, 2 positive rows).
2. One PR per repo for everything, or A+C first and B (two migrations, external output files) on its own?
3. AS400 / regulatory / LOS outputs: once order is stored, "first title" may differ from what was already sent
   for existing appraisals only if their titles are re-saved. Acceptable?

## Order of work

A (done) → C → B. C is mechanical and removes fields B would otherwise carry; B is the large one.

## Review — 2026-10-01

Built (BE by agent `be-title-order`, FE and review by lead). Nothing committed, no migration applied.

Migrations (not applied): Request `20260930171322_AddSequenceNumberToRequestTitles`; Appraisal
`20260930171137_DropUnusedDetailPriceColumns` (Building/Condo `SellingPrice`, `ForcedSalePrice`; Land
`EncroachmentArea`), `20260930171441_AddSequenceNumberToLandTitles`.

Decisions made while building
- Condo `TotalBuildingArea` kept: pricing (`buildFinalValue` and three other initialisers) uses
  `property.totalBuildingArea ?? usableArea`, fed by the condo GET.
- Request-title reads sort `SequenceNumber, CreatedAt, Id` so rows created before the column keep the
  request page's current order (lead changed the agent's `SequenceNumber, Id` in 8 reads).
- Data correction does not diff `SequenceNumber`: a pure reorder is not audited and is rejected as no change.
- `Backfill_LandAreaDeductionFromEncroachment.sql` (one-time, journaled by name) now guards its INSERT with
  `COL_LENGTH`. **Pre-check on UAT/prod before shipping** — non-zero means that script has not run and the
  drop would lose the figures:
  `SELECT COUNT(*) FROM appraisal.LandAppraisalDetails lad WHERE IsEncroached=1 AND EncroachmentArea>0 AND NOT EXISTS (SELECT 1 FROM appraisal.LandAreaDeductions d WHERE d.LandAppraisalDetailId=lad.Id)`
- No view / procedure needed a change: the ones reading LandTitles aggregate (SUM/AVG); `vw_PropertyGroupDetail`
  sorts title numbers alphabetically on purpose; `CollatrevTestFileBuilder` sorts by TitleNumber on purpose.

Follow-ups, not done
- `CollateralMasterUpsertService` picks the master deed from the first title — it can change for an appraisal whose
  titles are re-saved (reaches master dedup).
- `ProjectLandTitle` (block projects) has the same missing-order problem.
- Pending user answer: switch building to `BuildingInsurancePrice` vs drop the legacy column (recommended: drop,
  and point the 360 "insurance price" row at `buildingInsurancePriceOverride`).
- `CondoPriceDto` unused contract; `docs/data-model/03-appraisal-module.md`, `docs/datamart/cas-discovery.md` still list
  the dropped columns.

Verification: API build 0 errors; Appraisal.Tests 519/522 (3 known), Request.Tests 32/32, Reporting 54/54,
Collateral 179/179; full integration 38 failures identical by name to main, +3 new tests pass (mutation-checked);
FE tsc 0, vitest all pass.

## Review — 2026-10-01 (later): insurance redesign + /code-review rounds

User decisions after the first review
- Buildings: ONE column `BuildingInsurancePrice` holding the value shown on screen — the typed figure, else
  ROUND(SUM(IsBuilding rows' PriceAfterDepreciation, each at 2 dp), -3) midpoint away from zero; null only with no
  IsBuilding rows. `BuildingInsurancePriceOverride` renamed into it (legacy junk column dropped first; no UPDATE in the
  EF migration). The server computes on every save (`BuildingAppraisalDetail.ResolveInsurancePrice`, 8 create/update
  paths); the FE loads a stored value equal to the derived one as "not entered" (`utils/buildingInsurance.ts`).
  Accepted: a typed value equal to the derived one is indistinguishable. Condo keeps derived + override.
- One-time script `20261001090000_Backfill_BuildingInsurancePriceFromDepreciation.sql` fills existing buildings.
- EncroachmentArea: unmapped, NOT dropped in this release (the 2026-09-17 backfill still reads it; EF runs before
  DbUp). TODO beside `LandAppraisalDetailConfiguration` for the later hand-written drop + script guard.
- Condo `TotalBuildingArea` kept (pricing reads it).

/code-review: BE 6 rounds, FE 5 rounds. Fixed along the way: float vs decimal half-thousand in the FE rule; per-row 2 dp
on both sides; symmetric midpoint; correction confirm dialog shows the insurance the server will store; row ids on
surfaces / depreciation rows / periods (same zod-strip bug as titles); project land keeps encroachmentArea + landOffice;
removed fields trimmed from `shared/schemas/v1.ts`; LOS first title as OUTER APPLY TOP 1; `InDisplayOrder()` for
request titles; `AddTitle` numbers unnumbered titles last; `Titles` orders legacy ties by `SqlGuid` (= SQL Server);
`vw_PropertyGroupDetail` + `CollatrevTestFileBuilder` follow the entered order.
Deliberately left (see review rounds): three pre-existing title-sync copies; per-row insurance fallback in readers;
HasDefaultValue(0); legacy 0 + tie-breaks; RequestSyncService dropping unknown ids (pre-existing); condo 360 insurance
row shows the derived price (pre-existing); Building Cost Value keeps the float rule shared with ConstructionInspection
and pricing.

Deploy notes for the PR
- Destructive migrations (drops + a rename): stop all API nodes before the DB bundle, do not roll node by node.
- Down() of `UseBuildingInsurancePriceForAppraiserValue` is lossy: after a rollback every stored value reads as typed.
- Pre-check still recommended before a future release drops EncroachmentArea (query in the first review).

## Building Cost Value — 2026-10-01 (user: "แก้รวมกันไปเลย เอาแบบเดียวกับค่าประกัน")

- `FinalCostValueOverride` → `BuildingCostValue` (plain RenameColumn `UseBuildingCostValueForStoredFinalCost`), stores the
  on-screen figure: typed, else ROUND(SUM over ALL depreciation rows at 2 dp, -3) away from zero; null without rows.
  `ResolveDerivedValues()` resolves cost + insurance at the 8 building create/update paths. API field `buildingCostValue`.
- One-time `20261001090100_Backfill_BuildingCostValueFromDepreciation.sql` (sibling of the insurance backfill).
- SQL references renamed (user asked to watch views/SPs): `reporting.vw_MisCasReport` (the only DB object naming it),
  Maintenance `BackfillEngagementCurrentValue.sql`, raw SQL in decision summary, pricing data, construction current value,
  3 report providers. The journaled `20260919170000_DataFix_PricingAnalysisMethodRoleAndBuildingCost.sql` was edited
  (EF runs before DbUp and the prod bundle compiles journaled scripts under NOEXEC; one-time scripts are journaled by
  name, no checksum — checked in deploy/New-DbDeploymentScripts.ps1).
- FE: one server rounding rule `roundSumToThousand` in pricingAnalysis/domain/calculation.ts used by the property table,
  ConstructionInspection, pricing (`buildingFinalCostValue`) and the insurance; `enteredFigure` reads a stored figure
  equal to the derived one as not entered (forms + pricing BuildingCostTable).
- /code-review: BE round 7 clean (perf fix applied), FE round 8 no reachable bug (cleanups applied).
- Testing caveat: 3000/7111 now run from the `cetus` worktrees; `migrate` from this worktree renames/drops columns cetus
  still maps on the shared dev DB.

## Title reorder UI — 2026-10-01 (user: "ฝากแก้ทั้ง 2 หน้า property และ data correction")

Mock: FE `docs/poc/land-title-order-mock.html` (https://claude.ai/artifact/7KzktE9kVkzii72jAq2fiW), variant C built.
- FE `LandTitleTable`: ▲▼ per row (useFieldArray.move, focus follows the moved title), "เรียงตามเลขโฉนด"
  (numeric collation, blank numbers last, replace + re-validate), "โฉนดแรก" tag. Off on block project land
  (`TitleDeedForm orderable={false}` — ProjectLandTitle stores no order).
- Data correction: a reorder is ONE entry `Land.TitleOrder` (API SnapshotDiff + FE formDiff, same rule): kept titles
  out of relative order or a new title ahead of a kept one; labels by number with " (2)" for repeats; renumbered/new
  titles show their new number; no entry when both sides read the same. Reorder-only corrections are accepted.
- RS22 (gov-price list in the summary book) orders price, property, land, title order.
- /code-review: API to round 10, FE to round 11 — no correctness issues left. Left: pre-existing silent drop of an
  incoming title whose id matches nothing (LandDetailSync / PMA copies / RequestSyncService).
