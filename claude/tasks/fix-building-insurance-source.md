# Fix BuildingInsurance source in GetDecisionSummaryQueryHandler

## Tasks
- [x] Update `insuranceSql` query to read from `BuildingDepreciationDetails.PriceAfterDepreciation` where `IsBuilding = 1`
- [x] Verify build passes

## Review

### Change Summary
**File:** `Modules/Appraisal/Appraisal/Application/Features/DecisionSummary/GetDecisionSummary/GetDecisionSummaryQueryHandler.cs` (lines 33-39)

**Before:** The `insuranceSql` query summed `BuildingAppraisalDetails.BuildingInsurancePrice` — which is the wrong source for building insurance value.

**After:** The query now sums `BuildingDepreciationDetails.PriceAfterDepreciation`, joining through `BuildingAppraisalDetails` → `AppraisalProperties`, and filtering `bdd.IsBuilding = 1` to only include actual buildings (excluding fences, other structures, etc.).

### Why this matters
The building insurance value should reflect the depreciated price of actual building structures, not the raw `BuildingInsurancePrice` field. Non-building items (fences, other structures) flagged with `IsBuilding = 0` are excluded from the insurance calculation.

### Impact
- Single SQL query change — no schema, domain, or response type changes needed.
- The return type remains `decimal`, so all downstream mapping (result → response) is unaffected.
