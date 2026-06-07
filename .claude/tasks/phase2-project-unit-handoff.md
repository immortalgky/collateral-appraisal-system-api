# Phase 2: Block Reappraisal — Completion Handoff Writes Master Units

## Key Design Decisions

### ReplaceUnits — explicit delete, not nav-based orphan tracking
`FindProjectMasterByLastAppraisalIdAsync` includes `ProjectDetail` but NOT `ProjectDetail.Units`.
Relying on EF's collection change-tracking for orphan removal would silently fail (units survive) 
because the collection is not loaded. Instead: `ExecuteDeleteAsync` on `CollateralDbContext.ProjectUnits` 
before inserting, then set the new units via `ProjectDetail.ReplaceUnits` so the domain model is 
consistent with what EF will INSERT. This is explicit, predictable, and safe for both first-appraisal 
(no existing rows) and reappraisal (replaces set atomically in the same SaveChanges UoW).

### PurchaseBy string ↔ enum translation
Appraisal-side stores `UnitPurchaseMethod` as int in DB but exposes it as the enum type in C#.
The DTO `ProjectUnitForCollateral` will carry `string? PurchaseBy` (enum NAME: "Cash"/"Loan"/null).
Conversion in handler: `u.PurchaseBy?.ToString()`.
Reverse translation in upsert service: `Enum.TryParse<UnitPurchaseMethod>(dto.PurchaseBy, out var method)`.

### CustomerName source
`SELECT TOP 1 rc.Name FROM request.RequestCustomers rc WHERE rc.RequestId = @RequestId`
Same pattern as `vw_BlockMaintenanceList`. Fetched via the same `connectionFactory.QueryFirstOrDefaultAsync` 
call (or parallel, same injection). Only fetched for block-project appraisals.

### ProjectUnitPrice loading
`ProjectUnitPrices` are NOT in `GetWithFullGraphAsync`. Loaded separately via:
`dbContext.ProjectUnitPrices.AsNoTracking().Where(p => unitIds.Contains(p.ProjectUnitId)).ToListAsync()`
Then joined in-memory by `ProjectUnitId`. Only executed when project is non-null.

## Todo

- [x] 1. Extend `AppraisalForCollateralResult.cs`: add `PurchaseBy`/`LoanBankName`/`AppraisedValue` to `ProjectUnitForCollateral`; add `CustomerName` to root record
- [x] 2. Update `GetAppraisalForCollateralQueryHandler.cs`:
       - Load ProjectUnitPrices for the project's units
       - Include PurchaseBy/LoanBankName/AppraisedValue in MapProject
       - Add CustomerName sub-query (parallel Dapper call)
       - Pass CustomerName in return
- [x] 3. Extend `ProjectUpsertData` record: add `IReadOnlyList<ProjectUnit> Units` + `string? CustomerName`
- [x] 4. Add `SetCustomerName` domain method on `CollateralMaster`
- [x] 5. Update `CollateralMaster.UpsertFromProjectAppraisal`: call `ReplaceUnits` + `RecountRemaining` + `SetCustomerName`
- [x] 6. Update `UpsertProjectAsync` in `CollateralMasterUpsertService`:
       - Map DTO units → Collateral `ProjectUnit` entities (branch on ProjectType)
       - Set sale info (PurchaseBy translate) + LastAppraisedValue
       - ExecuteDeleteAsync existing units for reappraisal case
       - Pass Units + CustomerName into ProjectUpsertData
- [x] 7. Remove Phase 1 TODO comments
- [x] 8. `dotnet build` — confirm clean

## Review

### Files Modified

**Appraisal module:**
- `AppraisalForCollateralResult.cs` — added `CustomerName` (string?) to root record; added `PurchaseBy` (string?), `LoanBankName` (string?), `AppraisedValue` (decimal?) to `ProjectUnitForCollateral`
- `GetAppraisalForCollateralQueryHandler.cs` — `MapProject` now takes a `Dictionary<Guid, decimal?> unitPriceLookup`; loads `ProjectUnitPrices` via DbSet (units not navigable from Project aggregate); maps PurchaseBy via `.ToString()`; fetches CustomerName via TOP 1 Dapper sub-query from `request.RequestCustomers`; passes CustomerName in result

**Collateral module:**
- `CollateralMaster.cs` — extended `ProjectUpsertData` with `Units` + `CustomerName`; added `SetCustomerName` domain method; `UpsertFromProjectAppraisal` now calls `ReplaceUnits` + `RecountRemaining` + `SetCustomerName`; all Phase-1 TODO stubs removed
- `ICollateralMasterRepository.cs` — added `DeleteProjectUnitsAsync(Guid, CancellationToken)`
- `CollateralMasterRepository.cs` — implements `DeleteProjectUnitsAsync` via `ExecuteDeleteAsync` on `ProjectUnits` DbSet
- `CollateralMasterUpsertService.cs` — added `MapProjectUnits` static helper; `UpsertProjectAsync` calls `DeleteProjectUnitsAsync`, maps units, passes full `ProjectUpsertData`; Phase-1 TODO stub removed

### PurchaseBy string ↔ enum translation
- **Appraisal → DTO**: `u.PurchaseBy?.ToString()` — produces "Cash"/"Loan"/null from `Appraisal.Domain.Projects.UnitPurchaseMethod`
- **DTO → Collateral entity**: `Enum.TryParse<CollateralMasters.Models.UnitPurchaseMethod>(dto.PurchaseBy, out var method)` — works because both enums have identical names. On parse success → `SetSaleInfo(true, method, loanBankName)` (enforces Loan→LoanBankName invariant). On parse failure or null with IsSold=true → `MarkSold()` (bypasses invariant, user corrects via BUM screen).

### ReplaceUnits — explicit delete strategy
`FindProjectMasterByLastAppraisalIdAsync` includes `ProjectDetail` but NOT `ProjectDetail.Units`. If we relied on EF's collection change-tracking, old rows would silently survive (nav collection is empty = EF thinks there are no children to remove). Instead:
- `DeleteProjectUnitsAsync` issues `ExecuteDeleteAsync` (direct SQL DELETE, bypasses change-tracker) BEFORE `SaveChanges`
- `ReplaceUnits` then sets the in-memory collection so EF tracks the new rows as INSERT
- For first appraisal: ExecuteDeleteAsync deletes 0 rows (no-op), units are inserted fresh
- For reappraisal: old rows are deleted, new set is inserted atomically in the same UoW

### CustomerName sub-query
`SELECT TOP 1 rc.Name FROM request.RequestCustomers rc WHERE rc.RequestId = @RequestId`
Mirrors `vw_BlockMaintenanceList` and `vw_AppraisalList` patterns. Only executed when project is non-null. Uses existing `connectionFactory.QueryFirstOrDefaultAsync<string?>` injection (same as `RequestNumber` query).
