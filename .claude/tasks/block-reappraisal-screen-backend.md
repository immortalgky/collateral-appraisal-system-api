# Phase C — Block Reappraisal Screen Backend

## Plan

### Deliverables
1. [x] `Database/Scripts/Views/Collateral/vw_BlockReappraisalDueList.sql`
2. [x] `GetBlockReappraisalDueList` query + handler + DTO
3. [x] `GetBlockReappraisalDetail` query + handler + DTOs
4. [x] `MarkBlockReappraisalNotRequired` command + handler
5. [x] `BlockReappraisalEndpoints.cs` (Carter ICarterModule)
6. [x] `AuthDataSeed.cs` — add BLOCK_REAPPRAISAL_VIEW + BLOCK_REAPPRAISAL_CREATE permissions
7. [x] `MenuSeedData.cs` — add standalone menu node

## Files Created
- `Database/Scripts/Views/Collateral/vw_BlockReappraisalDueList.sql`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/GetBlockReappraisalDueList/GetBlockReappraisalDueListQuery.cs`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/GetBlockReappraisalDueList/GetBlockReappraisalDueListQueryHandler.cs`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/GetBlockReappraisalDetail/GetBlockReappraisalDetailQuery.cs`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/GetBlockReappraisalDetail/GetBlockReappraisalDetailQueryHandler.cs`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/MarkBlockReappraisalNotRequired/MarkBlockReappraisalNotRequiredCommand.cs`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/MarkBlockReappraisalNotRequired/MarkBlockReappraisalNotRequiredCommandHandler.cs`
- `Modules/Collateral/Collateral/Application/Features/BlockReappraisal/BlockReappraisalEndpoints.cs`

## Files Modified
- `Modules/Auth/Auth/Infrastructure/Seed/AuthDataSeed.cs`
- `Modules/Auth/Auth/Infrastructure/Seed/MenuSeedData.cs`

## Review
See below after implementation.
