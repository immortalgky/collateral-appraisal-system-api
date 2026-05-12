# Collateral Master Issue 2 — IsMaster Pattern Plan

## Checklist

- [x] 1. Domain: add `IsMaster` + `ParentMasterId` to `CollateralMaster.cs`; add `CreateLandAlias` factory; add invariant guards on `AppendEngagement` / `UpsertFromLandAppraisal`
- [x] 2. EF Config: update `CollateralMasterConfiguration.cs` with self-FK + index for `ParentMasterId`
- [x] 3. Migration: add `AddIsMasterAndParentMasterId` migration + snapshot update
- [x] 4. Repository: add `FindLandByDedupKeyIncludingAliases` (returns ANY row — master or alias — with the dedup key; caller resolves to master via `ParentMasterId`)
- [x] 5. Upsert service: replace single-title pick in `UpsertLandAsync` with multi-title group logic
- [x] 6. Lookup handler: navigate to master when hit lands on alias; load alias titles for result
- [x] 7. Lookup result: add `AliasTitles[]` optional field to `LandDetailDto`
- [x] 8. Catalog handler: add `AND IsMaster = 1` filter to SQL
- [x] 9. SQL view `vw_CollateralMasters`: add `AND m.IsMaster = 1` to WHERE
- [x] 10. SQL view `vw_CollateralEngagements`: verify engagements only join IsMaster (no code change needed; engagements only land on IsMaster by invariant)
- [x] 11. SnapshotBuilder: emit `titles[]` array for Land
- [x] 12. Tests: add 8 new tests in `CollateralUpsertServiceTests.cs`
- [x] 13. Build & verify all tests pass

## Review

### Changes Made

**Domain Layer**
- `CollateralMaster.cs`: Added `IsMaster` (bool, default true) and `ParentMasterId` (Guid?) with private setters. Added `CreateLandAlias` factory that creates alias rows (IsMaster=false) with dedup-key-only LandDetail. Added invariant guards: `AppendEngagement` and `UpsertFromLandAppraisal` both throw `InvalidOperationException` if called on an alias row.
- All existing factory methods (`CreateLand`, `CreateCondo`, `CreateLeasehold`, `CreateMachine`) default to `IsMaster=true, ParentMasterId=null`.

**EF Configuration**
- `CollateralMasterConfiguration.cs`: Added `IsMaster` property config (bit NOT NULL DEFAULT 1), `ParentMasterId` nullable config, self-FK with `OnDelete(DeleteBehavior.Restrict)`, and index on `IsMaster`.

**Migration**
- `AddIsMasterAndParentMasterId`: Adds two columns to `collateral.CollateralMasters`. Existing rows default to `IsMaster=1, ParentMasterId=NULL`. No data movement needed.

**Repository**
- `ICollateralMasterRepository`: Added `FindLandByDedupKeyIncludingAliases` which finds ANY row (master or alias) by dedup key. Callers resolve to master via `ParentMasterId`. Also added `FindByIdWithoutDetailsAsync` for lightweight master resolution.
- `CollateralMasterRepository`: Implemented both new methods.

**Upsert Service**
- `CollateralMasterUpsertService.UpsertLandAsync`: Replaced single-title pick with multi-title group algorithm per spec. For each title in the appraisal, looks up any existing row (master or alias), resolves to master, collects distinct master IDs. Empty → create new group (IsMaster row + alias rows). Exactly 1 → reuse master, create any missing aliases. More than 1 → `ConflictException`. Engagement and last-known update go to IsMaster only. Returns the IsMaster row for leasehold resolution.

**Lookup Handler**
- `LookupCollateralMasterQueryHandler`: When the Land dedup query hits an alias row (IsMaster=false), navigates to the master via ParentMasterId. Loads alias titles from the LandDetails of all rows with ParentMasterId = master.Id (or Id = master.Id). Returns alias titles in result.

**Lookup Result**
- `LandDetailDto`: Added optional `AliasTitles` property (list of dedup-key tuples for each alias).
- `LookupCollateralMasterResult`: Unchanged shape for non-Land types.

**Catalog Handler**
- Added `AND IsMaster = 1` to the WHERE clause so alias rows don't inflate counts.

**SQL Views**
- `vw_CollateralMasters.sql`: Added `AND m.IsMaster = 1` to WHERE clause. Alias rows are never shown.
- `vw_CollateralEngagements.sql`: No change needed; engagements only attach to IsMaster by domain invariant.

**Snapshot**
- `SnapshotBuilder.BuildLand`: Now emits `titles[]` array containing all titles from the appraisal property's `LandIdentity.Titles` collection.

**Tests (8 new)**
- `MultiTitle_3Titles_Creates1Master2Aliases_EngagementCount1`
- `MultiTitle_Reappraisal_SameTitles_NoNewAliases_EngagementCount2`
- `MultiTitle_Reappraisal_OneRemoved_RemovedAliasStays_EngagementCount2`
- `MultiTitle_Reappraisal_OneAdded_NewAliasCreated_EngagementCount2`
- `MultiTitle_LookupByAnyTitle_ReturnsMasterWithAllAliases`
- `MultiTitle_OverlapConflict_ThrowsConflictException`
- `Catalog_MultiTitle_Returns1Row`
- `Condo_Leasehold_Machine_AreSingletonGroups_IsMasterTrueParentMasterIdNull`
