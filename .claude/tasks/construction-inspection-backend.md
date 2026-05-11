# Construction Inspection Backend Implementation

## Tasks

- [x] 1. `AppraisalTypes.cs` — add `ConstructionInspection` constant + ValidValues
- [x] 2. `RequestSubmittedIntegrationEvent.cs` — add `PrevAppraisalId` + `AppraisalType`
- [x] 3. `AppraisalCreationRequestedIntegrationEvent.cs` — add same fields
- [x] 4. `RequestSubmittedEventHandler.cs` — populate new fields (Purpose=="06" → AppraisalType="ConstructionInspection")
- [x] 5. New file: `Collateral.Contracts/ConstructionInspection/GetMostRecentCompanyForAppraisalQuery.cs`
- [x] 6. New file: `Collateral/CollateralMasters/Application/ConstructionInspection/GetMostRecentCompanyForAppraisalQueryHandler.cs`
- [x] 7. `RequestSubmittedIntegrationEventConsumer.cs` — add CI forceCompanyId branch + update BuildCreationRequestedEvent
- [x] 8. `CompanySelectionActivity.cs` — handle forceCompanyId shortcut
- [x] 9. `AppraisalCreationRequestedIntegrationEventHandler.cs` — pass new fields
- [x] 10. `IAppraisalCreationService.cs` — add optional params
- [x] 11. `AppraisalCreationService.cs` — CI property deep-copy path

## Key Decisions
- CI detection: `Purpose == "06"` (AppraisalPurpose seed code "Inspect construction work")
- Force company: lookup CollateralEngagements.AppraisalCompanyId ordered by AppraisalDate DESC for given AppraisalId
- Property deep-copy: reuse existing CopyFrom static factory pattern from Appraisal.CopyProperty()
- EF load for prior appraisal: Include(Properties).ThenInclude each detail, plus LandDetail.Titles
- ConstructionInspection detail is NOT copied (stays empty for fresh CI tracking)

## Review
All 11 tasks implemented. CI flows end-to-end:
1. Request submitted with Purpose="06" + PrevAppraisalId set
2. Handler stamps AppraisalType="ConstructionInspection" on the integration event
3. Workflow consumer resolves the prior company via GetMostRecentCompanyForAppraisalQuery
4. forceCompanyId/Name seeded into workflow variables
5. CompanySelectionActivity short-circuits to forced company
6. Appraisal is created as type=ConstructionInspection with all properties deep-copied from prior appraisal
