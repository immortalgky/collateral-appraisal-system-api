# Phase B — BlockReappraisalDue Scan Job

## Plan

- [x] 1. Create `BlockReappraisalDue` entity in `Modules/Collateral/Collateral/CollateralMasters/Models/`
- [x] 2. Create `BlockReappraisalDueConfiguration` in `Modules/Collateral/Collateral/CollateralMasters/Configurations/`
- [x] 3. Add `DbSet<BlockReappraisalDue>` to `CollateralDbContext`
- [x] 4. Create `BlockReappraisalDueScanJob` in `Modules/Collateral/Collateral/CollateralMasters/Services/`
- [x] 5. Register `BlockReappraisalDueScanJob` as Scoped in `CollateralModule`
- [x] 6. Register recurring Hangfire job in `Bootstrapper/Api/Program.cs`
- [x] 7. Run `dotnet ef migrations add AddBlockReappraisalDue ...` (files only)
- [x] 8. `dotnet build` — 0 errors, 0 warnings confirmed

## Review

All deliverables implemented. Migration `20260603163812_AddBlockReappraisalDue` created and verified.
Build clean: 30 projects, 0 errors, 0 warnings.
