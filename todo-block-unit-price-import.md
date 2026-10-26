# Block project: import unit prices from Excel

Mock: https://claude.ai/artifact/R9VJYAVgEHFC5Qv2uBHhZW

## Goal

The Unit Price tab gets a project-level **price source**: `Calculated` (today's behaviour) or
`Imported` (the appraiser prices outside CAS and uploads the price list). Both Condo and LB.
Replaces the hand-run SQL patches in `Database/Scripts/Maintenance/PatchProjectUnitPrices_*.sql`.

## Decisions (user, 2026-10-16)

1. Imported prices are upload-only; to change a price, fix the Excel and upload again. No inline edit.
2. LB column headers: use the proposal below; the user will correct them if needed.
3. Forced-sale price per unit = the file's value (no % recompute).
4. Re-uploading the unit list while prices exist: warn, then clear the prices (cascade, as today).

## Root causes found

- `UnitPriceTab.tsx` `displayedUnitPrices` always runs `recomputeUnitPrice`, overwriting each row's
  `standardPrice`/`coverageAmount` from the model, so stored (patched) values never show.
- The table shows coverage as `coverageAmount × usableArea`, but BE stores the per-unit total.
- Save = `calculate`, which rewrites every unit.

## Column mapping

| Condo header (committee workbook) | LB header (proposal) | Target |
|---|---|---|
| ห้องชุด (key 1) | เลขแปลง (key 1) | `ProjectUnits.RoomNumber` / `PlotNumber` |
| ห้องชุดเลขที่ (key 2) | เลขที่บ้าน (key 2) | `CondoRegistrationNumber` / `HouseNumber` |
| ชั้นที่, อาคาร, แบบห้องชุด, พื้นที่ใช้สอย, ราคาขายโครงการ | แบบบ้าน, เนื้อที่ดิน (ตร.วา), พื้นที่ใช้สอย (ตร.ม.), ราคาขาย | compared and reported only, never written |
| ราคา (บาท/ตร.ม.) | ราคามาตรฐาน (บาท) | `StandardPrice` |
| ราคาประเมินที่เสนอขออนุมัติ | มูลค่าประเมิน | `TotalAppraisalValue` + `TotalAppraisalValueRounded` (as is) |
| ราคาบังคับขาย (บาท) | ราคาบังคับขาย (บาท) | `ForceSellingPrice` |
| มูลค่าประกันอัคคีภัย | มูลค่าประกันอัคคีภัย | `CoverageAmount` |

Parsing rules carried over from the SQL patches: the first sheet; row 1 = headers; drop the trailing
totals row but use it to check the sums; round numbers to 2 decimal places; match by key 1, then key 2;
blocking errors: a row matching two units, a unit claimed by two rows, or a row with no unit.

## Todo

### Backend (`dotnet-backend`), worktree `tilefish`
- [ ] B1 Domain: `Project.UnitPriceSource` (`Calculated` default / `Imported`) + last-import file name,
      time and user. `ProjectUnitPrice.ApplyImportedValues(...)` (clears flags, location/floor/land
      adjustments).
- [ ] B2 One EF migration for the new `Projects` columns (written, NOT applied; the user runs `migrate`).
- [ ] B3 `ProjectUnitPriceExcelParser` (Condo + LB) + matcher, pure functions with unit tests.
- [ ] B4 `POST /appraisals/{id}/project/unit-prices/import/preview`: parse + match, writes nothing,
      returns counts, blocking errors, attribute diffs, and the sheet total vs the sum.
- [ ] B5 `POST /appraisals/{id}/project/unit-prices/import`: re-parses the same file (stateless),
      refuses on any blocking error, writes prices, sets `Imported`, runs
      `AppraisalValuationSummaryService.RecomputeAsync` in the same transaction (same as calculate).
- [ ] B6 `GET /appraisals/{id}/project/unit-prices/template`: xlsx of current units + stored prices with
      the headers above, so a download → edit → upload round trip always matches.
- [ ] B7 `calculate` sets the source back to `Calculated`. Expose `unitPriceSource` + last import info
      on the unit-prices GET.
- [ ] B8 Permission/ownership: same guard as the existing unit-price endpoints; the file size limit
      matches the unit-list upload.

### Frontend (`react-expert` + `test-automator`), new FE worktree
- [ ] F1 API hooks: preview, import, template download.
- [ ] F2 UnitPriceTab source toggle. Imported: show DB values as is, hide the assumption form, flags
      and Save buttons, import card + upload dialog (preview → confirm). Switching to Calculated asks
      for confirmation first.
- [ ] F3 Calculated mode: show the DB values while nothing is edited; live preview only when the form
      or a flag is dirty. Fix the coverage column so it does not multiply a stored total by area.
- [ ] F4 UnitListingTab: when the project has prices, warn before re-uploading that they will be cleared.
- [ ] F5 i18n th/en/zh (run the full vitest suite: locale parity).

### QA (`test-automator`)
- [ ] Q1 BE build + tests, FE tsc + full vitest.
- [ ] Q2 E2E on the user's running 3000/7111 against a test fixture appraisal (Condo and LB): upload →
      preview → import → values shown match the file → ValuationAnalyses total updated → switch back
      to calculate → re-upload the units clears the prices. Screenshots.

5. Block appraisal-level forced sale = SUM of the units' `ForceSellingPrice`, always (both modes):
   the % already went into each unit's price. Today it is `total × rate`.
6. Block totals (appraised value, forced sale, insurance) sum **every unit, sold or not**. Today every
   rollup filters `IsSold = 0`.

### Block rollups (decisions 5 + 6), `dotnet-backend`
- [ ] B9 `AppraisalValuationSummaryService` block branch: drop `!pu.IsSold`; `ForcedSaleValue` =
      SUM(`ForceSellingPrice`) instead of `total × rate`; also stop INNER JOINing `ProjectModels`, which
      silently drops units with a NULL model (gotcha_block_rollup_inner_joins_project_model_id).
- [ ] B10 `GetDecisionSummaryQueryHandler` block SQL (KEEP IN SYNC pair): drop `pu.IsSold = 0`;
      per-model FSP = SUM of its units' `ForceSellingPrice`; units with no model get a row of their own.
- [ ] B11 Same `IsSold` filter removed from: `SaveDecisionSummaryCommandHandler` (line ~158),
      `GetProjectUnitPricesQueryHandler` (the Unit Price grid, so its total row matches the book; sold
      units get a "sold" chip), reporting `AppraisalSummaryBlockDataProvider` (book unit list).
- Untouched: LOS `GetAppraisalResultQueryHandler` (already all units, per-unit sum),
  `vw_BlockMaintenanceList` (collateral master, not an appraisal total), reappraisal matching.
- Consequences: a block's appraised value rises on any appraisal with sold units, and that value picks
  the approval tier / committee. The Decision Summary force-sale **rate override no longer changes a
  block's forced-sale total**. Existing block appraisals keep their old `ValuationAnalyses` figure
  until their prices are next saved or imported (no backfill).

## Code-review triage

Declined on purpose (do not re-raise):
- Guard on `SaveProjectUnitPrices` when the source is Imported: the FE's Imported→Calculated Save
  saves flags before `calculate`, so a guard would break it; the FE hides Save Draft in that state.
- ReplaceUnits recomputing `ValuationAnalyses`: pre-existing behaviour, out of scope.
- Units with neither key: known limitation (left out of the template).
- ReplaceUnits resetting `UnitPriceSource` (round 3 re-raise): Imported with no file shows the
  "no price file yet" state, which is the intended prompt to upload again.
- FE preview reading the live `ProjectModel` (fire-insurance code, standard land area, ModelType
  matching) instead of the saved model-assumption snapshot: drift predates this feature.
- Round 4: an execution-strategy retry after a successful `SaveChanges` leaves entities tracked as
  Unchanged, so the retry writes nothing. This affects every `ITransactionalCommand`; the fix belongs
  in `TransactionalBehavior` (clear the change tracker between attempts). **Follow-up, not this PR.**
- Import/calculate handlers writing `ProjectUnitPrices` through the DbContext instead of a repository:
  follows the existing `CalculateProjectUnitPricesCommandHandler` precedent (raised rounds 2 and 5).
- Matched rows with all prices blank are listed with the units missing from the file (round 6): both
  end with the price cleared, and the FE label "units without a price in the file" covers both.

## Review

Status 2026-10-18: implemented, reviewed clean (BE round 7, FE round 26 plus one post-E2E delta round), E2E passed.
Nothing committed.

**What was built**
- BE: `Project.UnitPriceSource` + import metadata (one EF migration); `ImportProjectUnitPrices/` (parser, analyzer,
  template builder); endpoints `import/preview`, `import`, `template`, `source`; `calculate` resets the source to
  `Calculated`; `ReplaceUnits` clears the import metadata. Block rollups sum every unit (sold or not), and forced sale =
  SUM(unit FSP). Book Verification scales forced sale proportionally when the typed total differs. One-time DbUp
  script backfills the per-unit FSP on in-progress block appraisals.
- FE: source toggle on the Unit Price tab; Imported view shows stored values as they are; import card + preview
  dialog; Calculated view previews with BE rounding parity (`decimalRounding.ts`) and marks rows that differ from
  what is stored; unit-list re-upload warns before clearing prices; the Decision Summary rate override is hidden for blocks.

**Verification**
- BE: build clean; Appraisal unit 838 pass, integration 139 pass; `UnitPriceDecimalStorageTests` pins decimal(18,2)
  rounding half away from zero.
- FE: tsc 0; vitest 2473 pass; build ok; eslint: only the 2 pre-existing `any` in DecisionSummaryPage.
- E2E 2026-10-18 on 7111/3000 (tilefish), fixtures 69105490 (Condo) and 69105491 (LB): template → preview (good, bad,
  totals mismatch) → import (file = screen = DB = ValuationAnalyses = Decision Summary) → back to Calculated →
  unit re-upload warning. 0 aborted writes. Migration + backfill applied to the dev DB (user-authorised).
- Post-E2E fixes: money always 2 decimals in the dialog/card; English plurals ("1 unit"); zh unit counts translated.

**Known, not fixed (follow-ups)**
- Persisted `ProjectModelAssumptions` rows: the pricing-assumptions DTO returns the snapshot's fire-insurance code
  and land figure while `Project.cs` calculates from the live model, so the FE preview differs from stored coverage.
  The UI never writes these rows (0 in the dev DB), so it is reachable only from a direct API call. Pre-existing.
- After `ReplaceUnits`, `ValuationAnalyses` keeps the old total until the next import/calculate (pre-existing).
- Reappraisal unit upload keeps existing units (and their prices) by design: no warning, nothing cleared.
- `TransactionalBehavior` retry after a successful SaveChanges (see triage).
