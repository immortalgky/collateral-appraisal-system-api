# Phase 4: Move Block Unit Maintenance to Collateral Module

## Key Decisions
- Route paths unchanged: GET/PUT /block-unit-maintenance[/{collateralMasterId:guid}/units]
- Auth: `.RequireAuthorization()` (bare) — same as old endpoints
- Key change: projectId → collateralMasterId everywhere
- PurchaseBy in the new command: `UnitPurchaseMethod?` from Collateral module (not Appraisal)
- Save path: `ICollateralMasterRepository.SaveChangesAsync()` (no separate UoW)
- Old Appraisal BUM folder + vw_BlockMaintenanceList.sql get deleted after new code compiles

## Todo

- [x] 1. Create `Database/Scripts/Views/Collateral/vw_BlockMaintenanceList.sql`
- [x] 2. Create Collateral BUM feature folder structure
- [x] 3. `GetBlockUnitMaintenanceList` query + handler + DTO (Dapper, paginated)
- [x] 4. `GetBlockUnitMaintenanceUnits` query + handler + DTOs (multi-result Dapper)
- [x] 5. `UpdateProjectUnitSaleInfo` command + handler + validator + request
- [x] 6. `BlockUnitMaintenanceEndpoints` (Carter, Collateral namespace)
- [x] 7. Delete old Appraisal BUM feature folder
- [x] 8. Delete `Database/Scripts/Views/Appraisal/vw_BlockMaintenanceList.sql`
- [x] 9. `dotnet build` — confirm clean

## Review
<!-- filled after implementation -->
