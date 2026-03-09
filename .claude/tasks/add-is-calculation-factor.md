# Add IsCalculationFactor to ComparativeAnalysisTemplateFactor

## Todo
- [x] 1. Domain — Add `IsCalculationFactor` to `ComparativeAnalysisTemplateFactor.cs`
- [x] 2. Domain — Add `isCalculationFactor` param to `ComparativeAnalysisTemplate.AddFactor()`
- [x] 3. EF Config — Add property config in `ComparativeAnalysisTemplateFactorConfiguration`
- [x] 4. AddFactorToTemplate — Update Request, Command, Handler, Result, Response, Endpoint
- [x] 5. GetComparativeAnalysisTemplateById — Split Factors into ComparativeFactors + CalculationFactors
- [x] 6. EF Migration — Generate migration
- [x] 7. Build verification — `dotnet build` passes (0 errors)

## Review

### Summary of Changes
Added `IsCalculationFactor` boolean property to `ComparativeAnalysisTemplateFactor` to distinguish between comparative factors (all factors in a template) and calculation factors (the subset used for score calculation with weights/intensity).

### Files Modified (12 total)

**Domain (2 files)**
- `ComparativeAnalysisTemplateFactor.cs` — Added `IsCalculationFactor` property, updated `Create()` and `Update()` methods
- `ComparativeAnalysisTemplate.cs` — Added `isCalculationFactor` parameter to `AddFactor()`

**Infrastructure (2 files)**
- `ComparativeAnalysisTemplateConfiguration.cs` — Added EF config: `.IsRequired().HasDefaultValue(false)`
- Migration `AddIsCalculationFactorToTemplateFactor` — Adds `BIT NOT NULL DEFAULT 0` column

**AddFactorToTemplate feature (6 files)**
- Request, Command, Handler, Result, Response, Endpoint — threaded `IsCalculationFactor` through the full stack

**GetComparativeAnalysisTemplateById (3 files)**
- Result — Changed single `Factors` list to `ComparativeFactors` (all) + `CalculationFactors` (filtered subset)
- Response — Same split
- QueryHandler — Maps all factors to `ComparativeFactors`, filters `IsCalculationFactor == true` for `CalculationFactors`

### Breaking API Change
- `GET /comparative-analysis-templates/{id}` response shape changed: `factors[]` → `comparativeFactors[]` + `calculationFactors[]`
- `POST /comparative-analysis-templates/{id}/factors` request now accepts optional `isCalculationFactor` (defaults to `false`, backward-compatible)
