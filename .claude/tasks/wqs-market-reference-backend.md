# WQS/SG/DC as Reusable Market Reference — Backend Implementation

## Plan

### Section 1: Merge PricingAnalysis anchor
- [x] Extend `PricingAnalysisSubjectType` enum (PropertyGroup=0..ProfitRentRef=6)
- [x] Replace `PropertyGroupId`/`ProjectModelId` with `AnchorId`+`AnchorRefKey`+`HostMethodId`
- [x] Keep `CreateForPropertyGroup`/`CreateForProjectModel`; add `CreateForReference`
- [x] Update `CloneForGroup` to use `AnchorId`
- [x] `SetFinalAppraisedValueInternal`: switch on SubjectType; ref types fire no event
- [x] EF config: drop old indexes/FK/CHECK; add composite filtered-unique + new CHECK
- [x] Migration: 20260603100000_MergePricingAnalysisAnchor (Up+Down+Designer)

### Section 1a: Repository + guards
- [x] Switch 4 GetBy*/ExistsBy* methods to `SubjectType + AnchorId`
- [x] Add `DeleteByHostMethodIdsAsync`, `DeleteReferencesByAnchorAsync`, `GetReferencesByAnchorAsync`
- [x] Update `AppraisalFinalValuesChangedEventHandler` filter to use `SubjectType==PropertyGroup && AnchorId`
- [x] `DeleteProjectModelCommandHandler`: delete subject PA after RemoveModel (app-level FK guard)
- [x] Update `IPricingAnalysisRepository` interface

### Section 1b: Active cleanup service
- [x] New `Application/Services/PricingReferenceCleanupService.cs`
- [x] Hook into `DeletePropertyGroupCommandHandler`
- [x] Hook into `DeleteProjectModelCommandHandler`
- [x] Hook into `DeletePropertyCommandHandler`
- [x] Hook into `RemoveMethodCommandHandler`
- [x] Hook into `SaveIncomeAnalysisCommandHandler` (room name reconcile)
- [x] Register in DI

### Section 2: Persist manual subject value
- [x] Add `CollateralValue` to `PricingComparativeFactor`
- [x] EF config: add column mapping
- [x] Thread through `SaveComparativeAnalysis` request DTO + handler
- [x] Migration: 20260603110000_AddCollateralValueToComparativeFactor (Up+Down+Designer)

### Section 3: Reference endpoints
- [x] `CreateOrGetReference` (POST): find-or-create idempotently; pre-add Market approach
- [x] `GetReferences` (GET): list by anchor

### Build verification
- [x] `dotnet build` — 0 errors (full solution, 30 projects)
- [x] `dotnet ef migrations list --no-connect` — 20260603100000_MergePricingAnalysisAnchor + 20260603110000_AddCollateralValueToComparativeFactor visible

---

## Review

### What was changed

**Domain layer (`Domain/Appraisals/`):**
- `PricingAnalysis.cs`: Enum extended (7 values); `PropertyGroupId`/`ProjectModelId` replaced by `AnchorId`+`AnchorRefKey`+`HostMethodId`; 3 factory methods + updated `CloneForGroup`; `SetFinalAppraisedValueInternal` switches on SubjectType.
- `PricingComparativeFactor.cs`: `CollateralValue` column added; `Create`/`Update` updated.
- `IPricingAnalysisRepository.cs`: 3 new methods added; 4 existing updated.

**Infrastructure layer:**
- `PricingConfiguration.cs`: Old indexes/FK/CHECK dropped; new composite filtered-unique + CHECK added.
- `PricingAnalysisRepository.cs`: 4 methods rewritten; 3 new methods added.
- 2 migration pairs: `MergePricingAnalysisAnchor`, `AddCollateralValueToComparativeFactor`.

**Application layer:**
- `AppraisalFinalValuesChangedEventHandler.cs`: Filter changed to `SubjectType==PropertyGroup && AnchorId`.
- `PricingReferenceCleanupService.cs`: New service with 5 cleanup methods.
- `DeletePropertyGroupCommandHandler.cs`: Cleanup hook added.
- `DeleteProjectModelCommandHandler.cs`: Cleanup hook + app-level FK guard added.
- `DeletePropertyCommandHandler.cs`: Cleanup hook added.
- `RemoveMethodCommandHandler.cs`: Cleanup hook added.
- `SaveIncomeAnalysisCommandHandler.cs`: Room-name reconcile hook added.
- `SaveComparativeAnalysis/` request+handler: `CollateralValue` threaded through.
- New feature folders: `CreateOrGetReference/`, `GetReferences/`.
- `AppraisalModule.cs`: `PricingReferenceCleanupService` registered.
