# Auto-calculate BuySellYear from MarketComparable.SaleDate

## Plan
- [x] Add `ComputeTimeFromSaleDate()` and `ComputeCumulativeAdjPeriod()` to `PricingCalculationHelper.cs`
- [x] Update `SaveComparativeAnalysisCommandHandler` to inject `IMarketComparableRepository`, auto-compute time adjustment before recalculation
- [x] Update `LinkComparableCommandHandler` to seed BuySellYear/Month on link
- [x] Build and verify no errors

## Review

### Changes Made (3 files)

1. **`Domain/Services/PricingCalculationHelper.cs`** — Added two static methods:
   - `ComputeTimeFromSaleDate(DateTime saleDate)` → returns `(int Years, int Months)`
   - `ComputeCumulativeAdjPeriod(int years, decimal? adjustedPeriodPct)` → returns `decimal`

2. **`SaveComparativeAnalysisCommandHandler.cs`** — Injected `IMarketComparableRepository`. Added Step 3.5 `ApplyTimeAdjustmentsFromSaleDate()` that batch-loads comparables by ID, computes BuySellYear/Month from SaleDate, and recalculates CumulativeAdjPeriod before the recalculation service runs.

3. **`LinkComparableCommandHandler.cs`** — After seeding offer/sale price, also seeds BuySellYear/Month from SaleDate when linking a comparable.

### Security: No issues — all values are server-computed from existing DB data, no user input used for time calculation.
### Frontend impact: `BuySellYear`/`BuySellMonth` in `CalculationInput` are still accepted but ignored — backend always overwrites from SaleDate.
