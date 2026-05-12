# Hypothesis Cost-of-Building FSD Compliance (B01-B11)

## Goal
Add 8 per-row B-fields to `HypothesisCostItem` for CostOfBuilding rows, compute B03/B06/B07/B08 in the calc service, expose B09/B10/B11 per-model totals in the model aggregate, plumb through save/preview/get DTOs, migrate DB.

## Plan

- [x] 1. Domain: Add B01-B08 fields to `HypothesisCostItem` + `SetBuildingCostInputs()` + `SetBuildingCostComputedFields()` mutation methods
- [x] 2. EF config: Add precision mappings for the new fields in `HypothesisCostItemConfiguration`
- [x] 3. Migration: `20260502051829_AddHypothesisCostItemBuildingFields` — 8 additive AddColumn ops, no destructive ops
- [x] 4. Calculation service: `ComputeBuildingDepreciation` private method computes B03/B06/B07/B08; B09/B10/B11 on `LandBuildingModelAggregate`; C19 now sources from B11 (`ValueAfterDepreciation ?? Amount`)
- [x] 5. DTOs: Extend `CostItemDto` with 8 new fields; extend `LandBuildingModelAggregate` with B09/B10/B11; extend `PreviewHypothesisAnalysisResult` with `CostItems`
- [x] 6. Get handler: Project new 8 B-fields
- [x] 7. Save command: Extend `HypothesisCostItemInput` with B01/B02/B04/B05 inputs; `SyncCostItems` calls `SetBuildingCostInputs`; validator adds range rules for CostOfBuilding rows
- [x] 8. Preview handler: `BuildTransientCostItems` calls `SetBuildingCostInputs`; response includes `CostItems` with computed B03/B06/B07/B08
- [x] 9. Tests: Added `CostOfBuilding_BFields_ComputedCorrectly_TwoModels` (2 models × 2 rows, cap check) + `CostOfBuilding_NullInputs_ComputedFieldsAreNull`
- [x] 10. Build: 0 errors; Hypothesis tests: 23/23 passed; 12 pre-existing income failures unchanged
- [x] 11. Migration applied: Done

## Review

Added FSD §2.1.3.5.1 Figure 52 B-field support to the Cost-of-Building tab.

### Key decisions
- B-fields are nullable on `HypothesisCostItem` so non-CostOfBuilding rows carry no overhead
- Backward compatibility: C19 (per-unit building value fed into dev-cost calc) falls back to `item.Amount` for pre-migration rows where `ValueAfterDepreciation` is null
- `ComputeBuildingDepreciation` is called inside `ComputeLandBuildingCore` before the per-model loop so B03/B06/B07/B08 are ready when B09/B10/B11 are aggregated
- All-null input guard preserves the "row not yet configured" state — computed fields stay null rather than storing zeros
- Preview result now includes `CostItems` list so FE gets B03/B06/B07/B08 without a separate save round-trip
