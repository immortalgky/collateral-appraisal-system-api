# Step 3 Plan: Upsert Service + AppraisalCompletedConsumer + Idempotency Tests

## Current State
- Step 1 done: CollateralDbContext, EF configs, migration, models, events all exist.
- Step 2 done: GetAppraisalForCollateralQuery handler returns full property data.
- AppraisalCompletedIntegrationEvent already has AppraisalId.

## Todo List

### Part A — MissingIdentityKeyException
- [x] Create `Modules/Collateral/Collateral/CollateralMasters/Exceptions/MissingIdentityKeyException.cs`

### Part B — UpsertFromAppraisal behavior on CollateralMaster aggregate
- [x] Add `UpsertFromAppraisal(...)` type-dispatch method on `CollateralMaster`
  - Land: calls `LandDetail.UpdateLastKnown(...)` + `LandDetail.UpdateAppraisalSummary(...)` + construction tracking + ConstructionStatusChangedEvent when flag flips
  - Condo: calls `CondoDetail.UpdateLastKnown(...)` + `CondoDetail.UpdateAppraisalSummary(...)`
  - Leasehold: calls `LeaseholdDetail.UpdateLastKnown(...)` + `LeaseholdDetail.UpdateAppraisalSummary(...)`
  - Machine: calls `MachineDetail.UpdateLastKnown(...)` + `MachineDetail.UpdateAppraisalSummary(...)` + promotion if incoming reg no arrives
  Note: AppendEngagement is ALREADY on the aggregate — keep as is.

### Part C — Repository type-aware lookups
- [x] Extend `ICollateralMasterRepository` with:
  - `FindLandByDedupKey(...)` 
  - `FindCondoByDedupKey(...)`
  - `FindLeaseholdByDedupKey(...)`
  - `FindMachineForUpsert(...)` (tier-1-then-tier-2 with promotion)
- [x] Implement in `CollateralMasterRepository`

### Part D — Snapshot JSON builder
- [x] Create `Modules/Collateral/Collateral/CollateralMasters/Services/SnapshotBuilder.cs` with static methods per type

### Part E — ICollateralMasterUpsertService
- [x] Create interface at `Modules/Collateral/Collateral/CollateralMasters/Services/ICollateralMasterUpsertService.cs`
- [x] Implement `CollateralMasterUpsertService.cs`:
  - Uses `ISender` (MediatR) to send `GetAppraisalForCollateralQuery`
  - Validation gate per type
  - Pass 1: Land, Condo, Machine
  - Pass 2: Leasehold (depends on underlying master)
  - Idempotency: catch DbUpdateException on unique constraint violation → treat as no-op
  - Register in CollateralModule.cs as scoped

### Part F — AppraisalCompletedConsumer
- [x] Create `Modules/Collateral/Collateral/CollateralMasters/Consumers/AppraisalCompletedConsumer.cs`
  - Implements `IConsumer<AppraisalCompletedIntegrationEvent>`
  - Calls `ICollateralMasterUpsertService.ProcessAppraisalAsync`
  - Exceptions propagate to MassTransit for dead-letter
  - Auto-registered via consumers assembly scan in Program.cs (need to verify that Collateral assembly is scanned)

### Part G — Register consumer in Program.cs / CollateralModule.cs
- [x] Verify consumer auto-registration (check Bootstrapper/Api/Program.cs)
- [x] Add `ICollateralMasterUpsertService` to CollateralModule.cs

### Part H — Integration tests in existing Integration.csproj
- [x] Create `Tests/Integration/Collateral.Integration.Tests/` directory and test files
- [x] Add `Modules/Collateral/Collateral` ProjectReference to Integration.csproj if missing
- [x] Create `CollateralUpsertServiceTests.cs` with tests 1-19
  - Tests 1-18 actively implemented
  - Test 19 (RESTRICT delete) deferred with comment (needs admin endpoint)
- [x] Tests call `ICollateralMasterUpsertService` directly via DI scope from fixture

### Part I — Build verification
- [x] `dotnet build` clean
- [x] `dotnet test Tests/Integration/Integration.csproj` passing

## Deviations
- The test project uses the existing combined Integration.csproj (not a separate Collateral.Integration.Tests.csproj) — tests are in Tests/Integration/Collateral.Integration.Tests/ folder inside the shared project.
- Tests call the upsert service directly (not via HTTP) since there's no endpoint yet — this is the correct approach for unit/service-level integration tests.
- AppraisalId already on AppraisalCompletedIntegrationEvent — no change needed.
