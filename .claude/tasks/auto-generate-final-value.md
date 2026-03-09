# Auto-generate PricingFinalValue for All Methods during SaveComparativeAnalysis

## Todo
- [x] Add auto-compute PricingFinalValue in `WqsCalculationService.cs` (RSQ final value)
- [x] Add auto-compute PricingFinalValue in `DirectComparisonCalculationService.cs` (min of TotalAdjustedValue)
- [x] Verify build passes (0 errors)
- [x] Fix WQS final value mismatch (2791666.67 vs 2398333.33)
- [x] Fix WeightedScore formula (remove /100 division to match frontend)
- [x] Fix confidence interval formula (use ±SE from rounded value to match frontend)
- [x] Remove unused GetTCritical method (dead code cleanup)

## Review

### Changes Made

**5 files modified:**

1. **`WqsCalculationService.cs`** — Auto-populate PricingFinalValue moved INSIDE the regression block so it only runs when regression was freshly computed. Uses `rsq.FinalValue` directly. Confidence interval changed from `tCritical × SE` to `FinalValueRounded ± SE` to match frontend. Removed unused `GetTCritical` method.

2. **`DirectComparisonCalculationService.cs`** — Added auto-compute after the per-calculation loop. Takes the minimum `TotalAdjustedValue` across all calculations (most conservative approach).

3. **`PricingCalculation.cs`** — Added `ClearOfferingPrice()` and `ClearSellingPrice()` methods to allow clearing stale mutually exclusive price fields.

4. **`SaveComparativeAnalysisCommandHandler.cs`** — When SellingPrice is set without OfferingPrice, clears stale OfferingPrice fields (and vice versa). This prevents `ComputeInitialPrice` from using a stale OfferingPrice=0 instead of the intended SellingPrice path.

5. **`PricingFactorScore.cs`** — Fixed `CalculateWeightedScore` to use `Score × FactorWeight` (removed `/100m` division that caused 100x mismatch with frontend).

### Bugs Fixed

1. **WQS final value mismatch (2791666.67 vs 2398333.33)**: Survey 3 had stale `OfferingPrice=0` in DB. `ComputeInitialPrice` hit Path A before Path C, returning 0 instead of 2,360,000. Fix: mutual exclusivity clearing in UpdateCalculations.

2. **WeightedScore 100x mismatch**: Backend had `Score × (FactorWeight / 100)` = 0.2, frontend expected `Score × FactorWeight` = 20. This caused slope=200000 instead of 2000. Fix: removed `/100m`.

3. **Confidence interval mismatch**: Backend used `tCritical × SE` (proper 95% CI), frontend used `±SE` from rounded value. Fix: changed to `FinalValueRounded ± SE`.

### Security
- No user input is used directly — values are computed from existing domain data
- No new endpoints or external surface area added
