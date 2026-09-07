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

---

# Addendum — 2026-09-06: the split became two blocks, not two columns everywhere

## Why

The trigger above is per **report** (`model.has_under_construction`), but the layout it turned on was
applied per **table**: every row in the collateral grid gained the second value cell. A finished
building therefore printed its one figure twice, under headings that read
`เมื่อแล้วเสร็จ 100%` / `ตามสภาพปัจจุบัน` — asserting it was still being built. The original
verification table above already recorded the symptom without naming it as a defect:

> a second, uninspected building shows `3,267,000` both sides

Users asked for finished and under-construction buildings to be shown as separate sections, the way
land and สิ่งปลูกสร้าง already are.

## Layout now

Inside a Cost group the สิ่งปลูกสร้าง block splits in two, in this order:

```
☑ ที่ดิน                              …                     value        (colspans the pair)
                                      รวมมูลค่าที่ดิน          value        (colspans the pair)
☑ สิ่งปลูกสร้าง                        finished buildings    value        (colspans the pair)
                                      ส่วนพัฒนา (finished)   value        (colspans the pair)
                                      รวมมูลค่าสิ่งปลูกสร้างก่อสร้างแล้วเสร็จ  value (colspans the pair)
                                                            เมื่อแล้วเสร็จ 100% | ตามสภาพปัจจุบัน
☑ สิ่งปลูกสร้าง (แบบแปลน)              being built           100%            | current
                                      ส่วนพัฒนา (being built) 100%           | current
                                      รวมมูลค่าสิ่งปลูกสร้างตามแบบแปลน       100% | current
```

Decisions taken with the user (2026-09-06), after reviewing a before/after HTML mock. That mock
lives at `docs/poc/summary-uc-block-split-mock.html` on the feature branch only — `.gitignore:135`
excludes `docs/poc/` on purpose ("POC mocks belong to whichever feature branch picks them up, not to
the shared history"), so it is not in the shared history and this table below does not list it.

| # | Decision |
|---|---|
| 1 | Finished block first, under-construction block last. |
| 2 | The new row label is `BuildingRowLabel` + `" (แบบแปลน)"` — the noun still resolves through the CollateralType parameter map, only the qualifier is literal. A building that is not finished is valued from its plan, not from what stands today. |
| 3 | A ส่วนพัฒนา line follows **its own property's** inspection, so one group can print a ส่วนพัฒนา sub-block in both halves. |
| 4 | Blocks split in every case, but the **subtotal rules are unchanged** — multi-group still prints only รวมมูลค่าทรัพย์สินกลุ่ม N, so the two blocks there are separated by the ☑ label and the sub-header row alone. |
| 5 | Appraisal-level figures, the seven-row totals table and the committee block are untouched. |

## Membership rule

A line is under construction when its `AppraisalPropertyId` has a `ConstructionInspections` row whose
progress is `< 100` — new provider-local helper `IsUnderConstructionProperty`, the per-property form
of the `anyProgressBelow100` predicate that was already there. No new column, query or migration:
RS24 already returns the percent per property.

The **trigger** is deliberately unchanged: `anyProgressBelow100 && constructionShortfall > 0`. The
shortfall term is what keeps a market/combined group out of the split (see the Deviation section
above), and `BuildingSubtotal` / `BuildingSubtotalCurrent` therefore still span the whole group —
narrowing them to one block would zero the shortfall and switch the split off entirely.

## Files

| File | Change |
|---|---|
| `Models/AppraisalSummaryModel.cs` | `SummaryGroupRow`: `BuildingsCompleted` / `DevelopmentItemsCompleted` / `BuildingsUnderConstruction` / `DevelopmentItemsUnderConstruction` (a partition of the existing flat lists), `BuildingSubtotalCompleted`, `BuildingSubtotalUnderConstruction(+Current)`, `BuildingRowLabelUnderConstruction` |
| `Providers/AppraisalSummaryLandBuildingDataProvider.cs` | `IsUnderConstructionProperty`; building rows now carry that flag through a tuple like the ส่วนพัฒนา rows already did; per-block subtotals; the qualified label |
| `partials/summary-standard-body.html` | The building region became `{{ if uc }}` two blocks `{{ else }}` the original single block `{{ end }}`; the group-total row moved out of the old `else if` chain into its own `{{ if multi }}` |
| `partials/summary-styles.html` | Refreshed the stale `tr.uc-head` comment, and added `tr.uc-head td.blank { border-bottom:0 }` + `tr.uc-open td:nth-child(-n+4) { border-top:0 }` for the downward merge |
| `Providers/AppraisalSummaryCondoDataProvider.cs` | Zero floors / age / ชั้นที่ print nothing; new `IsStated` helper for the free-text floor |

No migration, no frontend, no contract change, no SQL view. `approver-block.html` needed no edit.

Settled in review and in testing with the user:

- **Item numbering restarts in each block** (`done_b.size > 1` / `uc_b.size > 1`, each loop from 1) —
  user decision, 2026-09-06. Continuous numbering across both blocks was tried first, on the reasoning
  that the two blocks are one list of buildings; the user asked for a restart, and each block is
  captioned and totalled as its own list on the form, so the number reads as a position within that
  list. The "only number when there is more than one" rule therefore counts the block.
- **Both subtotal captions name their block**, per the reference form the user supplied
  (2026-09-06): `รวมมูลค่าสิ่งปลูกสร้างก่อสร้างแล้วเสร็จ` and `รวมมูลค่าสิ่งปลูกสร้างตามแบบแปลน`.
  An earlier cut left the finished block on the plain historic `รวมมูลค่าสิ่งปลูกสร้าง`, which review
  flagged as ambiguous — it used to mean *all* of the group's buildings, so a checker could reconcile
  land + that row and come up short. Both now say which block they close. Note the caption is not the
  row label plus a qualifier: the row reads `(แบบแปลน)` while its caption reads `ตามแบบแปลน`.
- **Subtotal captions stay literal, row labels stay parameter-driven.** Every `รวมมูลค่า…` caption in
  this table is a literal — `รวมมูลค่าที่ดิน` does not become `รวมมูลค่าสิทธิการเช่าที่ดิน` for a
  leasehold group either — so the new one is written out in full rather than interpolating
  `BuildingRowLabelUnderConstruction`. Interpolating it would also leave the caption reading a bare
  `รวมมูลค่า` whenever CollateralType 05 is missing from the parameter master. The ☑ row label is
  still resolved through that map, which is what decision #2 is about.
- **The multi-group `รวมมูลค่าทรัพย์สินกลุ่ม N` row keeps both value cells — do not merge them.**
  Review flagged it as the last place a finished group still prints "the same figure twice", and a
  colspan was tried; it is wrong and was reverted. Unlike every other merged cell in this table the
  two figures are not equal by construction: `GroupTotal` is the **stored** valuation
  (`GroupAppraisalValue ?? AppraisalPrice ?? FinalValueRounded`) while `GroupTotalCurrent` is summed
  from the components the grid actually printed — deliberately, so the grid cannot be contradicted
  by a stale `GroupValuations` row. Merging the cells hides the second figure, and it is the one
  `รวมมูลค่าทรัพย์สินตามสภาพปัจจุบัน` adds up, so the ตามสภาพปัจจุบัน column stops reconciling with
  its own grand total. A comment on the row now records this.
- **The sub-header row keeps four separate blank cells, and merges DOWNWARD instead.** The labels
  describe only the ราคาประเมิน pair, so the other four columns carry nothing — but collapsing them
  into one `colspan="4"` cell was tried and rejected: it deleted the column rules, which the form
  keeps. What the strip must not have is the *horizontal* rule under it, which boxed it in as a row
  of its own above the ☑ label. So `.blank` drops those four cells' bottom border and `.uc-open` —
  set on the first row of the under-construction block, whether that is a building row or the
  ส่วนพัฒนา header — drops the top border of its first four cells. The two right-hand cells keep
  their full box, so the labels still read as a header. Same mechanism as `grp-first`/`cont`.
- **A floor count or building age of 0 prints nothing** (`is { } x && x != 0`), in the land-building
  and condo summaries alike — the block summary already did this. The form has no way to say "not
  stated", so an unentered 0 came out as `0 ชั้น` / `อายุอาคาร 0 ปี`, which reads as a fact. Condo's
  `FloorNumber` is free text, so `IsStated` rejects a blank, a dash placeholder and a numeric zero
  there while keeping non-numeric floors such as "G". Note this changes reports that have nothing
  under construction — it is a description-text fix, not part of the split. A building left with
  nothing at all to describe now prints an empty cell rather than a bare list number, so the number
  never appears without a description beside it.

  Two instances of the same shape are deliberately NOT changed: `พื้นที่ใช้สอย 0 ตารางเมตร` in these
  same lines, and `ExternalBookBuilder.cs`'s `{NumberOfFloors} ชั้น` on the external book's building
  details. Both were raised with the user; the request was scoped to floors and age on the appraisal
  summary, and neither was included.
- **The current value is capped at 100% progress.** `RS24.ProgressPct` is `SUM(CurrentProportionPct)`
  over work details, which nothing bounds. Above 100 the row counts as finished (so it prints in the
  completed block, single cell) while an uncapped current value still inflated the subtotals — the
  printed blocks would then not add up to `รวมมูลค่าทรัพย์สินตามสภาพปัจจุบัน`, which is exactly the
  reconciliation the two captions promise.
- **Each block and its subtotal are gated on that block having rows.** The `.grp-total` box exists
  to close a bordered block; over an empty block it would draw a stray grey strip. The single-block
  branch keeps its historic unconditional subtotal — it is the untouched form, and a report without
  the split must print exactly what it printed before.

## Verified by rendering

Two instances of the API side by side — pristine `origin/main` on 7113, this branch on 7112 — HTML
compared after stripping comments and normalising whitespace. Every response asserted `http=200`
and non-empty. A checker asserted that every row of every `table.grid` spans the same number of
columns once colspans are counted (`ragged tables: 0` throughout).

| Case | Result |
|---|---|
| 69000178 — Cost, one finished + one at 8.75% | The defect case. Finished building now prints `3,267,000.00` once across the pair; the 8.75% one sits below the sub-header at `3,267,000.00 / 285,862.50`. Block subtotals `3,267,000.00` and `3,267,000.00 / 285,862.50` re-sum to the old combined `6,534,000.00 / 3,552,862.50` |
| 69000178 — totals + committee | Byte-identical to main (`28,928,862.50` / `20,250,203.75` / `32,000,000.00` / `6,534,000.00` / `22,400,000.00`) |
| 69000103 — Cost, 50%, committee | Only building is under construction → the finished block and its subtotal are omitted entirely; committee block unchanged |
| 69000098 — market, under construction | Identical to main: split still suppressed, 5 columns |
| 11 further appraisals with an inspection below 100% but priced by market (69000125, 69000004, 69000074, 69105455, 69000071, 69000110, 69105465) and 4 with no inspection at all (APP-20260221-64654182, APP-20260220-0F38D400, APP-20260219-701D5F72, APP-20260216-FD6580AA) | All identical to main |
| `appraisal-book` 69000178 (standard body, 15mm margins) | Same two-block structure |
| `appraisal-summary-{condo,machine,construction}` | Identical to main |
| `appraisal-summary` composite, PDF | 200, rasterised and read — the printed form matches the agreed mock |

Dev data holds only two appraisals that actually turn the split on, and neither carries ส่วนพัฒนา on
both sides, an enhancement-only group, or more than one group. Those template branches were rendered
instead from hand-built `AppraisalSummaryModel`s through `ScribanTemplateRenderer.RenderRawAsync`
(a throwaway `Reporting.Tests` console harness, not committed — the assembly already grants it
`InternalsVisibleTo`): single-mixed, single-enhancement-only, single-under-construction-only,
multi-group with the split on, multi-group with it off, and a market/combined group placed after a
split group (it takes the other branch and assigns none of the block variables — proof that nothing
leaks across loop iterations in Scriban's global scope). All seven rendered with
`ragged tables: 0`, item numbering restarting at 1 in each block, and the ☑
marker correctly moving onto the ส่วนพัฒนา header row in each block of the enhancement-only case.
