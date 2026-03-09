# Persist Template Selection on PricingAnalysisMethod

## Todo

- [x] 1. Domain: Add `ComparativeAnalysisTemplateId` property + setter to `PricingAnalysisMethod`
- [x] 2. EF Config: Add FK relationship in `PricingAnalysisMethodConfiguration`
- [x] 3. SaveComparativeAnalysis: Thread `ComparativeAnalysisTemplateId` through Request → Command → Endpoint → Handler
- [x] 4. GET responses: Return `ComparativeAnalysisTemplateId` in `GetComparativeFactors` and `GetPricingAnalysis`
- [x] 5. EF Migration: Generate migration
- [x] 6. Build verification: `dotnet build` passes (0 errors, 29 warnings — all pre-existing)

## Review

### Summary
Added `ComparativeAnalysisTemplateId` as a nullable FK on `PricingAnalysisMethod` so the frontend can persist which template was selected when building a comparative analysis.

### Files Changed (9 files + 1 migration)

1. **Domain/Appraisals/PricingAnalysisMethod.cs** — Added `Guid? ComparativeAnalysisTemplateId` property + `SetComparativeAnalysisTemplate(Guid?)` setter
2. **Infrastructure/Configurations/PricingConfiguration.cs** — Added `HasOne<ComparativeAnalysisTemplate>()` FK with `SetNull` on delete + using import
3. **SaveComparativeAnalysis/SaveComparativeAnalysisRequest.cs** — Added optional `ComparativeAnalysisTemplateId` param
4. **SaveComparativeAnalysis/SaveComparativeAnalysisCommand.cs** — Added optional `ComparativeAnalysisTemplateId` param
5. **SaveComparativeAnalysis/SaveComparativeAnalysisEndpoint.cs** — Passes `request.ComparativeAnalysisTemplateId` to command
6. **SaveComparativeAnalysis/SaveComparativeAnalysisCommandHandler.cs** — Calls `method.SetComparativeAnalysisTemplate()` after finding the method
7. **GetComparativeFactors/GetComparativeFactorsResult.cs** — Added `ComparativeAnalysisTemplateId` to result record
8. **GetComparativeFactors/GetComparativeFactorsResponse.cs** — Added `ComparativeAnalysisTemplateId` to response record
9. **GetComparativeFactors/GetComparativeFactorsQueryHandler.cs** — Passes `method.ComparativeAnalysisTemplateId` to result
10. **GetPricingAnalysis/GetPricingAnalysisQueryHandler.cs** — Added `ComparativeAnalysisTemplateId` to `MethodDto` + its instantiation
11. **Migration: 20260308111324_AddComparativeAnalysisTemplateIdToPricingMethod.cs** — Adds nullable column, index, FK with SetNull

### Security Review
- No user input validation needed — it's a nullable FK; the DB enforces referential integrity
- SetNull on delete prevents orphan references
- No sensitive data exposed
