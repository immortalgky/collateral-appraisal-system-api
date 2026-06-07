# Block Project → Collateral Master (Phase A)

## Goal
Register block Project (condo-block / LandAndBuilding-village) into CollateralMaster on appraisal completion.

## Todo

- [x] 1. Add `ProjectForCollateral` DTOs to `AppraisalForCollateralResult.cs`
- [x] 2. Add `PrevAppraisalId` to `AppraisalForCollateralResult` root record
- [x] 3. Extend `GetAppraisalForCollateralQueryHandler` to populate Project + PrevAppraisalId
- [x] 4. Add `CollateralTypes.Project = "PRJ"` constant
- [x] 5. Create `ProjectDetail.cs` domain entity (mirror CondoDetail)
- [x] 6. Add `ProjectUpsertData` record to `CollateralMaster.cs`
- [x] 7. Add `CreateProject` factory + `UpsertFromProjectAppraisal` + `ExcludeFromReappraisal` to `CollateralMaster.cs`
- [x] 8. Add `ProjectDetail` to `SyncIsDeletedToDetails`
- [x] 9. Create `ProjectDetailConfiguration.cs` (EF config)
- [x] 10. Wire `ProjectDetail` 1:1 navigation into `CollateralMasterConfiguration.cs`
- [x] 11. Map 3 new `CollateralMaster` reappraisal exclusion columns in config
- [x] 12. Add `FindProjectMasterByLastAppraisalIdAsync` to interface + impl
- [x] 13. Add `UpsertProjectAsync` private method to `CollateralMasterUpsertService`
- [x] 14. Call `UpsertProjectAsync` at the top of `ProcessAppraisalAsync` (before property loop)
- [x] 15. Generate/hand-author EF migration `AddProjectCollateralAndReappraisalFlag`
- [x] 16. Run `dotnet build` — 0 errors

## Review

- Added `ProjectForCollateral` record hierarchy to the Appraisal→Collateral DTO contract.
- `GetAppraisalForCollateralQueryHandler` injects `IProjectRepository`, loads full project graph, maps to DTO.
- New `CollateralTypes.Project = "PRJ"` constant.
- New `ProjectDetail` entity mirrors `CondoDetail`: private ctor, internal ctor, `UpdateStructure`, `UpdateAppraisalSummary`, `SetIsDeleted`.
- `CollateralMaster.CreateProject` factory, `UpsertFromProjectAppraisal(ProjectUpsertData)`, `ExcludeFromReappraisal/IncludeInReappraisal` mutators.
- `ProjectDetailConfiguration` maps `collateral.ProjectDetails` table with owned `AppraisalSummary`.
- `ICollateralMasterRepository.FindProjectMasterByLastAppraisalIdAsync` — finds existing PRJ master via AppraisalSummary.LastAppraisalId for lineage dedup.
- `CollateralMasterUpsertService.UpsertProjectAsync` — runs BEFORE the property loop, shares the single `SaveChangesAsync`.
- EF migration adds `ProjectDetails` table + 3 reappraisal-exclusion columns on `CollateralMasters`.
