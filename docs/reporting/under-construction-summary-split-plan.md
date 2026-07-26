# Plan — Under-construction split on the Land & Building appraisal summary

**Report:** `appraisal-summary-land-building` (via `partials/summary-standard-body.html`).
Also reached by `appraisal-book.html` (internal "standard" body) — the change lands there too.

**Goal:** when the appraisal contains a building under construction, the ราคาประเมิน column
splits into **เมื่อแล้วเสร็จ 100%** / **ตามสภาพปัจจุบัน**, the totals block gains
current-condition rows, and the committee block shows both figures.

---

## Decisions (confirmed 2026-07-26)

| # | Decision |
|---|---|
| 1 | Stored cost value **is** the 100%-complete value. Current = stored × progress percent. |
| 2 | Scope = Land & Building summary only. Condo / Machine / Block untouched. |
| 3 | ส่วนพัฒนา items are their own property records — each carries **its own** inspection. No schema change. |
| 4 | Totals become **separate rows**, not two columns (per the reference image). |
| 5 | The progress percent lives in **`appraisal.ConstructionInspections`**, per `AppraisalPropertyId` — separate from the building. `BuildingAppraisalDetails.ConstructionCompletionPercent` is NOT the source (it reads 100.00 on all 27 dev rows). |

## Data already in place — no migration needed

- `appraisal.ConstructionInspections`, keyed by `AppraisalPropertyId`: `TotalValue` (the 100% value),
  `IsFullDetail`, `SummaryCurrentProgressPct` / `SummaryCurrentValue`.
- `appraisal.ConstructionWorkDetails` (child): `CurrentProportionPct`, `CurrentPropertyValue`.
- `appraisal.BuildingDepreciationDetails.PriceAfterDepreciation`, keyed by `BuildingAppraisalDetailId`,
  `IsBuilding=1` for the building line and `IsBuilding=0` for ส่วนพัฒนา lines.

Verified against dev data: `ci.TotalValue` equals the sum of that property's `PriceAfterDepreciation`
on every row, and `CurrentPropertyValue` equals `TotalValue × pct` exactly — so the per-line
`× pct` approach below is consistent with the inspection's own current value.

## Trigger rule

Any property in the appraisal with an inspection whose overall progress `< 100` — the same predicate
`CollateralMasterUpsertService.cs:745` already uses (`ci is not null &&
ci.OverallCurrentProgressPercent < 100m`), so the two stay in step — **AND** a non-zero shortfall in
at least one split (cost) group. See the Review section for why the second condition was added.

Dev test cases: **69000178 @ 10%**, 69000103 @ 50% (cost, with committee), 69000101 @ 60%,
69105452 @ 15%, several @ 0%. 69000098 @ 50% is market-priced — the split correctly stays off.

## Derivation

Per property `p` that has an inspection, reusing the canonical formula already defined on
`ConstructionInspection.OverallCurrentProgressPercent` (`ConstructionInspection.cs:238`):

```
pct_p       = IsFullDetail ? Σ wd.CurrentProportionPct  : SummaryCurrentProgressPct ?? 0
current_p   = IsFullDetail ? Σ wd.CurrentPropertyValue  : SummaryCurrentValue       ?? 0
value100_p  = ci.TotalValue        ( == Σ PriceAfterDepreciation of p's lines )
shortfall_p = value100_p − current_p
```

Each depreciation line `d` of property `p` renders `d.PriceAfterDepreciation` in the 100% column and
`d.PriceAfterDepreciation × pct_p / 100` in the current column — "-" when `pct_p = 0`
(ยังไม่ก่อสร้าง). Properties with no inspection are complete: both columns show the same value.

Appraisal-level (`TotalAppraisalValue` comes from `ValuationAnalyses.AppraisedValue`, i.e. the
100% figure — so subtract the shortfall rather than re-summing):

```
currentTotal      = TotalAppraisalValue − Σ shortfall_b
currentForcedSale = ForcedSaleValue × (currentTotal / TotalAppraisalValue)     -- proportional
```

Proportional forced-sale reproduces the reference doc exactly (723,370,000 / 1,434,855,000 =
1,033,385,000 / 2,049,793,000 = 0.50414). Falls back to `forceSaleRate` when the 100% total is
null/zero. `ทุนประกันภัยสิ่งปลูกสร้าง` (`ValuationAnalyses.InsuranceValue`) is **not** split.

## Layout

**Grid** — ราคาประเมิน becomes two sub-columns. A sub-header row (`เมื่อแล้วเสร็จ100%` /
`ตามสภาพปัจจุบัน`) is emitted immediately above each building block, matching the reference.
Land rows and รวมมูลค่าที่ดิน `colspan=2` across both (land is never under construction).
Building lines and รวมมูลค่าสิ่งปลูกสร้าง fill both. Description gains a
`(แล้วเสร็จ 47.07%)` / `(ยังไม่ก่อสร้าง)` suffix.

**Totals table** — when the split is on, rows become:

```
รวมมูลค่าทรัพย์สินตามสภาพปัจจุบันเป็นเงินทั้งสิ้น   <currentTotal>
  ( <baht text of currentTotal> )
ราคาบังคับขายตามสภาพปัจจุบัน                      <currentForcedSale>
รวมราคาประเมินทรัพย์สินเมื่อแล้วเสร็จ 100% เป็นเงินทั้งสิ้น  <TotalAppraisalValue>
  ( <baht text of TotalAppraisalValue> )
ทุนประกันภัยสิ่งปลูกสร้าง                          <InsuranceValue>
ราคาบังคับขายเป็นเงินทั้งสิ้น                       <ForcedSaleValue>
```

Otherwise the existing four rows are unchanged.

**Committee block** (`partials/approver-block.html`, shared by every summary) — when the split is
on, the single ราคาประเมิน row becomes two, each with baht text:
`ราคาประเมินตามสภาพปัจจุบัน` and `ราคาประเมินที่ดินพร้อมสิ่งปลูกสร้างแล้วเสร็จ 100%`.
Gated on a model flag only the land-building provider sets, so other bodies are unaffected.

---

## Todo

- [x] **1. SQL** — new result set in `AppraisalSummaryLandBuildingDataProvider`: per-`AppraisalPropertyId`
      inspection progress + current value (the `IsFullDetail` CASE, mirroring
      `AppraisalSummaryConstructionDataProvider` RS01). Add `AppraisalPropertyId` to RS14 (buildings)
      and RS18 (depreciation) so lines can be keyed back to their inspection.
- [x] **2. Row classes** — add `AppraisalPropertyId` to `GroupBuildingRow` / `GroupDepreciationRow`;
      new `ConstructionProgressRow`.
- [x] **3. Model** — `SummaryItemRow.CurrentValue`; `SummaryGroupRow.BuildingSubtotalCurrent`;
      `AppraisalSummaryModel.HasUnderConstruction`, `.CurrentConditionTotal`,
      `.CurrentConditionForcedSale`.
- [x] **4. Provider** — apply each property's inspection percent to its building and ส่วนพัฒนา lines,
      compute subtotals, and the appraisal-level current total / forced sale.
      Append the `(แล้วเสร็จ …%)` suffix in `BuildBuildingLine` / `BuildItemDesc`.
- [x] **5. Template — grid** (`summary-standard-body.html`): sub-header row, `colspan=2` land rows,
      second value cell on building + ส่วนพัฒนา + building-subtotal rows. All gated on
      `model.has_under_construction` so the existing 5-column form is untouched otherwise.
- [x] **6. Template — totals**: the seven-row variant above.
- [x] **7. Template — committee** (`approver-block.html`): two value rows when the flag is set.
- [x] **8. Styles** (`summary-styles.html`): widths for the split column pair; keep the
      `.totals.grid-aligned` divider aligned with the new right-hand column.
- [x] **9. Verify** — build, then render before/after PDFs on a second instance (port 7112).
      69000178 (@10%) exercises the split directly; a non-inspection appraisal confirms the
      existing 5-column form is unchanged.
- [x] **10. Review section** — append the summary of changes to this file.

## Not in scope

- Frontend: none. The progress percent is already captured by the construction-inspection screen.
- Condo / Machine / Block summaries, and the CI (ตรวจงานก่อสร้าง) report, which has its own
  100%/current fields (`BuildingValue100`).
- No new per-item percent on `BuildingDepreciationDetails` (decision 3).

## Review

Implemented 2026-07-26. Reporting module only — no migration, no frontend, no contract change.

### Files

| File | Change |
|---|---|
| `Providers/AppraisalSummaryLandBuildingDataProvider.cs` | RS24 (construction progress); `AppraisalPropertyId` on RS14/RS18; `ProgressPctOf` / `CurrentValueOf` / `ProgressSuffix` helpers; per-item current values; appraisal-level current total + forced sale |
| `Models/AppraisalSummaryModel.cs` | `SummaryItemRow.CurrentValue`; `SummaryGroupRow.BuildingSubtotalCurrent` / `.GroupTotalCurrent`; `HasUnderConstruction` / `CurrentConditionTotal` / `CurrentConditionForcedSaleValue` |
| `partials/summary-standard-body.html` | `colgroup`, `uc` gate, sub-header row, second value cell, seven-row totals variant |
| `partials/approver-block.html` | two committee value rows when the flag is set |
| `partials/summary-styles.html` | `table.grid tr.uc-head td` |

### Deviation from the plan — the trigger gained a second condition

The plan triggered purely on "some inspection below 100%". Rendering 69000098 (a **market**-priced
group whose building is 50% built) exposed the flaw: a market/combined group states one blended
value, so the shortfall cannot be attributed to it and nothing is deducted. The report printed
`ตามสภาพปัจจุบัน 5,000,000` beside `เมื่อแล้วเสร็จ 100% 5,000,000` and a committee block with two
identical figures — asserting the collateral is worth its finished value today, which is false.

The trigger now also requires `constructionShortfall > 0`, so an appraisal whose under-construction
buildings all sit in market groups keeps the single-value layout. The alternative — deducting a
cost-derived shortfall from a market-derived value — was rejected as not defensible.

Those buildings do not go unmarked: the market/combined building clause now carries the same
`(แล้วเสร็จ 50%)` suffix, so the reader still sees the building is unfinished.

### Verified by rendering (2nd instance, port 7112)

| Case | Result |
|---|---|
| 69000178 — cost, 10% | 6 columns, every row 6 cells wide; `3,267,000 → 326,700`; a second, uninspected building shows `3,267,000` both sides; subtotal `6,534,000 / 3,593,700`; total `28,643,000 → 25,702,700`; forced sale `20,050,100 → 17,991,890` (ratio preserved to the cent) |
| 69000103 — cost, 50%, committee | split renders; `1,940,000 → 970,000`; total `322,000,000 → 321,030,000`; committee shows both rows |
| 69000098 — market, 50% | split suppressed (5 columns, original 3 totals labels, single committee row); description keeps `(แล้วเสร็จ 50%)` |
| APP-20260221-64654182 — no inspection | 5 columns, every row 5 cells wide, no `uc-head`, totals back to the original 4 rows with the original labels |
| Condo + machine summaries | render 200, unaffected (`has_under_construction` false → `else` branch) |

### Known data caveat (not a code issue)

69000178's `ValuationAnalyses.AppraisedValue` (28,643,000) predates the second building added to it
mid-session, so its grand total is smaller than land + both buildings. The current-condition figure
is derived from whatever the stored total is, so it inherits any such staleness rather than
introducing it.
