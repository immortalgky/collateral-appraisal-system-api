# Phase D — Create Block Reappraisal

## Goal
Single-project reappraisal request creation triggered from the block-reappraisal screen.

## Decisions
- Command: `CreateBlockReappraisalCommand` in `Request.Application.Features.Reappraisal.CreateBlockReappraisal`
- Entry point: `POST /block-reappraisal/{collateralMasterId:guid}/create` in `Collateral/BlockReappraisalEndpoints.cs`
- PrevAppraisalId: resolved from `collateral.ProjectDetails` via Dapper in the Collateral handler
- Snapshot copy: reuses `FetchPriorRequestSnapshotsAsync` logic (extracted private Dapper helpers inline)
- Dedupe: reuses `FindInFlightAppraisalIdsAsync` SQL
- Channel: "SIBS" (TODO confirm for internal flow)
- Purpose: "03" (TODO confirm)
- User: `ICurrentUserService` in endpoint → `UserId = Username` (bank code from "name" claim)

## TODOs
- [x] Create `CreateBlockReappraisalCommand.cs`
- [x] Create `CreateBlockReappraisalResult.cs`
- [x] Create `CreateBlockReappraisalCommandHandler.cs`
- [x] Add `POST /block-reappraisal/{collateralMasterId:guid}/create` to `BlockReappraisalEndpoints.cs`
- [x] Add GlobalUsing for new namespace in Request module
- [x] `dotnet build` → 0 errors

## Review
All 5 files created/modified. Build: 0 errors.

Entry point: `BlockReappraisalCreateEndpoint` in Bootstrapper (not Collateral module).
Reason: Collateral.csproj does not reference Request module (only Request.Contracts). Moving
the endpoint to Bootstrapper avoids adding a module-to-module reference and matches the exact
pattern of ReappraisalEndpoints.cs which also lives in Bootstrapper.

PrevAppraisalId: Resolved from `collateral.ProjectDetails.LastAppraisalId` via Dapper single-row query.
Purpose/Channel: "03" / "SIBS" — both marked TODO(confirm).
Dedupe: Reuses the same SQL as InitiateReappraisalCommandHandler (FindInFlightAppraisalIdsAsync).
Snapshot: Reuses the same SQL as InitiateReappraisalCommandHandler (FetchPriorRequestSnapshotsAsync).
No large duplication — SQL strings are verbatim copies of the existing logic (could be extracted
to a shared helper later if desired).
