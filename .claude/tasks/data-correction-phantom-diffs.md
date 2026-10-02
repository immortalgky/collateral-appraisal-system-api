# Data correction lists fields nobody edited

The confirm dialog (and in two cases the audit) listed changes the admin never made. Surveyed read-only
on six real Completed appraisals plus test fixtures; four causes, all pre-existing.

| # | Shown | Cause | Reached DB / audit |
|---|---|---|---|
| 1 | Total Depreciation Percent Per Year — → 1 | depreciation table computes the %/year column; API has no such field | no — every building, every time |
| 2 | Postcode / Dopa Postcode — → 10200 | LocationSelector looks the postcode up from the sub-district on open; properties do not store it | no |
| 3 | 7754.93 → 7754.9325 | table recomputes money to 3–4 dp; columns are decimal(18,2) | audit yes (every correction), DB no |
| 4 | Distance from electricity / encroaching area — → 0 | `disabledValue: 0` fills a blank disabled field on open | yes, once per record |

## Plan
- [x] FE `formDiff.ts`: skip `totalDepreciationPercentPerYear`, `postcode`, `dopaPostcode`
- [x] FE `formDiff.ts`: depreciation-table figures (money 2 places, area and percentages 4) are compared
      and shown as they will be stored — `toStoredUnits` in `calculation.ts`, which rounds the decimal
      text the payload carries half away from zero, as the API parses and SQL Server stores it
- [x] FE `calculation.ts`: `roundSumToThousand` uses the same `toStoredUnits`, so the derived insurance /
      Building Cost Value sum the same per-row figures the backend sums
- [x] BE `BuildingDepreciationDetail` / `BuildingDepreciationPeriod`: every column held at its stored
      scale (`Stored(value, places)`), so the correction snapshot equals what is written;
      `RoundedDepreciatedValue` no longer re-rounds rows
- [x] FE `configs/fields.ts`: `electricityDistance`, `encroachingOthersArea` — disabled means blank
      (user's rule): stand-in 0 → null. Government price per sq.m / sq.wa untouched — missing from
      survey means a price of 0.
- [x] One-time script `20261002120000_DataFix_ClearZeroStandInsUnderNoToggle`: 0 → NULL where the
      toggle is an explicit No, on the two property tables and the two block-project tables (dry run
      on dev, rolled back: 71 / 99 / 1 / 0 rows). Chosen by the user over clearing them in the form,
      which review showed re-dirties a Discard and every legacy record on open.
- [x] Tests: `BuildingDepreciationMoneyTests` (BE), `formDiff.test.ts` + `disabledValue.test.tsx` (FE)
- [x] Live check: fixture 69105473 and a read-only survey of real appraisals show only the field
      edited; a real depreciation change lists the same figures in the dialog and the audit
- [x] `/code-review` until clean

## Not in scope
- Eight text fields still take a "-" stand-in on open.
- Many real Completed appraisals cannot be corrected at all: required fields the old records lack
  (Dopa sub-district, building owner…) block the save.

## Review
- Root of items 1–2: the dialog diffs form state, and the page fills or computes those keys on open.
  Diffing the payload instead was checked and rejected — the captured payload carries them too.
- Root of item 3: the table computes in floating point, the columns are fixed-scale decimals. Rounding
  inside the table formulas was rejected (chained rounding moves some figures by 0.01); instead both
  sides compare and hold figures at the stored scale. The FE rounds the posted decimal text rather than
  the float, because that is what the server rounds (205277.62499999997 → .62).
- Item 4: a null stand-in keeps blanks blank; FormFields deliberately never overwrites a stored value
  on open, so the zeros already written are cleared once in the database instead. A form-side
  `forceWhenDisabled` was tried and removed (dirty on open, Discard loop, refetch skipped, and a
  NULL toggle mapped to false could wipe a real figure).
- Declined review suggestions: a generic EF interceptor / scale-aware snapshot (block project models are
  not data-correctable), and a single shared rounding helper across pricing and depreciation.
