# Hypothesis L&B Land Area From Titles

## Goal
Derive C01/C02/C10/C10A from system data (land titles) instead of user input.

## Tasks

- [x] Understand navigation: PricingAnalysis → PropertyGroupId → properties → LandAppraisalDetail.TotalLandAreaInSqWa
- [x] Identify existing helper: PricingPropertyDataService already sums land area from group via SQL+EF
- [x] Add `GetTotalLandAreaFromTitlesAsync` to `PricingPropertyDataService`
- [x] Update `HypothesisCalculationService.ComputeLandBuildingCore` signature to accept `decimal? titleSum`
- [x] Update calc: c01, c02, c10a, c10 all derived from title sum / model row sums
- [x] Wire title sum into SaveHypothesisAnalysisCommandHandler
- [x] Wire title sum into PreviewHypothesisAnalysisCommandHandler
- [x] Wire title sum into GetHypothesisAnalysisQueryHandler
- [x] Add `TotalLandAreaFromTitles` to GetHypothesisAnalysisResult
- [x] Add `TotalLandAreaFromTitles` to PreviewHypothesisAnalysisResult
- [x] Add 5 new tests to HypothesisCalculationServiceTests
- [x] Run tests and verify

## Review

All 5 new tests pass. All 33 HypothesisCalculationService tests pass.
The 12 pre-existing IncomeCalculationService failures are unrelated to this change.
