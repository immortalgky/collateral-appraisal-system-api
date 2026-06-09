# Phase 1: Block Reappraisal — First-Class ProjectUnit Master Table

## Goal
Replace opaque `StructureJson` blob in `ProjectDetail` with a proper per-unit master table
in the Collateral module. Phase 1 is schema/domain only — upsert wiring is Phase 2.

## PurchaseBy Enum Decision
Define `Collateral.CollateralMasters.Models.UnitPurchaseMethod { Cash=1, Loan=2 }` locally
in the Collateral module. Reason: the Appraisal enum lives in `Appraisal.Domain.Projects`
which is a sibling module with no shared contract assembly. Cross-module enum reference
violates bounded-context independence. Values are identical; stored as NAME string (consistent
with Appraisal convention).

## Todo

- [x] 1. Create `Modules/Collateral/Collateral/CollateralMasters/Models/UnitPurchaseMethod.cs` (Collateral-local enum)
- [x] 2. Create `Modules/Collateral/Collateral/CollateralMasters/Models/ProjectUnit.cs` (entity)
- [x] 3. Create `Modules/Collateral/Collateral/CollateralMasters/Configurations/ProjectUnitConfiguration.cs` (EF config)
- [x] 4. Modify `ProjectDetail.cs` — add `Units` collection, `ReplaceUnits`, `RecountRemaining`, remove `StructureJson` + its param from `UpdateStructure`
- [x] 5. Modify `CollateralMaster.cs` — add `CustomerName` property + set null in `CreateProject`; update `UpsertFromProjectAppraisal` to not pass `StructureJson`
- [x] 6. Modify `CollateralDbContext.cs` — add `DbSet<ProjectUnit> ProjectUnits`
- [x] 7. Modify `ProjectDetailConfiguration.cs` — remove StructureJson column mapping; add navigation to Units (1:N)
- [x] 8. Modify `CollateralMasterConfiguration.cs` — add `CustomerName` mapping
- [x] 9. Fix `CollateralMasterUpsertService.cs` — remove `StructureJson` from `ProjectUpsertData` construction; keep `structureJson` local var only for engagement snapshot (TODO Phase 2)
- [x] 10. Fix `CollateralMaster.cs` `UpsertFromProjectAppraisal` — remove `StructureJson` param from `ProjectUpsertData` record + `UpdateStructure` call
- [x] 11. Author EF migration `AddProjectUnitMasterAndCustomerName`
- [x] 12. Update migration snapshot
- [x] 13. Run `dotnet build` — confirm clean

## Review

### Files Created
- `Modules/Collateral/Collateral/CollateralMasters/Models/UnitPurchaseMethod.cs` — Collateral-local enum (Cash=1, Loan=2), stored as NAME string. Intentional copy of Appraisal enum; see enum-decision below.
- `Modules/Collateral/Collateral/CollateralMasters/Models/ProjectUnit.cs` — Entity<Guid>, all fields, CreateCondo/CreateLandAndBuilding factories, SetSaleInfo invariants, MarkSold convenience, SetLastAppraisedValue (internal).
- `Modules/Collateral/Collateral/CollateralMasters/Configurations/ProjectUnitConfiguration.cs` — table `collateral.ProjectUnits`, NEWSEQUENTIALID() fallback, PurchaseBy as enum NAME string, all precisions.
- `Modules/Collateral/Collateral/Migrations/20260605103204_AddProjectUnitMasterAndCustomerName.cs` — drops StructureJson, adds CustomerName, creates ProjectUnits table with FK+indexes.

### Files Modified
- `ProjectDetail.cs` — removed StructureJson property; added _units backing field, Units read-only list, ReplaceUnits, RecountRemaining; UpdateStructure param count reduced.
- `CollateralMaster.cs` — added CustomerName nullable property (set null in CreateProject); removed StructureJson from ProjectUpsertData record; UpdateStructure call trimmed; TODO Phase 2 comments added.
- `CollateralMasterConfiguration.cs` — added CustomerName mapping (nvarchar(200), nullable).
- `ProjectDetailConfiguration.cs` — removed StructureJson config; added HasMany Units navigation.
- `CollateralDbContext.cs` — added DbSet<ProjectUnit> ProjectUnits.
- `CollateralMasterUpsertService.cs` — removed StructureJson from ProjectUpsertData construction; structureJson local var retained for engagement snapshot (audit trail).

### PurchaseBy Enum Decision
Defined `Collateral.CollateralMasters.Models.UnitPurchaseMethod { Cash=1, Loan=2 }` as a **Collateral-module-local enum**. Rationale: `Appraisal.Domain.Projects.UnitPurchaseMethod` lives in a sibling bounded context with no shared contracts assembly. Cross-module enum import would violate context independence and create a hidden coupling. Values and names are identical so Phase 2 can translate via string name equality without a mapping table.

### Phase 2 Stubs (TODO-marked)
1. `ProjectUpsertData` record: add `IReadOnlyList<ProjectUnit> Units` parameter
2. `UpsertFromProjectAppraisal` in `CollateralMaster.cs`: call `ProjectDetail.ReplaceUnits(data.Units)` + `RecountRemaining()` + set `CustomerName`
3. `UpsertProjectAsync` in `CollateralMasterUpsertService.cs`: build `List<ProjectUnit>` from `proj.Units` (the Appraisal-side `ProjectForCollateralResult`) and include in `ProjectUpsertData`
