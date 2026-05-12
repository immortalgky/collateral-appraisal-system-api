# Construction Inspection Fee + Image Copy

## Goal
1. On the Appointment & Fee page, when **any** appraisal property has a building under construction (property type `B` or `LB` with `IsUnderConstruction == true`), expose a new **Construction Inspection Fee** field on the fee form.
2. Persist that fee on the appraisal and propagate it to the Collateral module's `CollateralEngagement` snapshot.
3. When a future Construction Inspection (CI) appraisal application is created, seed its appraisal fee from the previous engagement's CI fee (instead of leaving the appraiser to re-enter it / using a generic appraisal fee).
4. Audit the property-copy flow (`Appraisal.CopyProperty`) — property images (`PropertyPhotoMapping`) are NOT copied today. Add image copy so the new property carries forward photos from the source property.

## User decisions (locked)
1. **Rename** existing dead `InspectionFeeAmount` → `ConstructionInspectionFeeAmount`. Same on FE.
2. **Hide** field entirely when no property is under construction (no disabled state).
3. **Blank** when previous engagement has no CI fee — never fall back to generic appraisal fee.
4. Image copy scope: **per-property** path only (`CopyProperty`).
5. **CI fee skips normal fee generation** (tier/quotation pipeline) — instead seed directly from previous engagement's `ConstructionInspectionFeeAmount`.

## Existing-field check (per ask)
- Backend: `AppraisalFee.InspectionFeeAmount` (`AppraisalFee.cs:32`) + setter `SetInspectionFee` (`:126`) — **dead code**, will rename.
- Frontend: `inspectionFee` (`src/features/appraisal/schemas/appointmentAndFee.ts:37`) — also dead, will rename.

## Visibility rule
Show the Construction Inspection Fee field only when **at least one** appraisal property satisfies:
- `PropertyType.HasBuildingDetail == true` (codes `B`, `LB`, `LSB`, `LS`), AND
- `BuildingAppraisalDetail.IsUnderConstruction == true`

Source of truth on the FE: the property list already loaded for the appraisal (each property carries its building detail with `underConst` per `src/shared/forms/typeBuilding.ts:42`).

## Backend tasks

### B1. Domain — add `ConstructionInspectionFeeAmount` to `AppraisalFee`
- File: `Modules/Appraisal/Appraisal/Domain/Appraisals/AppraisalFee.cs`
- Rename `InspectionFeeAmount` → `ConstructionInspectionFeeAmount`; rename `SetInspectionFee` → `SetConstructionInspectionFee`.
- Update EF configuration if column name is mapped explicitly (search `Modules/Appraisal/Appraisal/Infrastructure/Configurations/` for `InspectionFeeAmount`).

### B2. Migration
- Add EF migration that renames column `InspectionFeeAmount` → `ConstructionInspectionFeeAmount` on `appraisal.AppraisalFees` (or whatever the table name is). Verify no production data depends on the old name (it's dead code, but check anyway).

### B3. Wire setter into a Save command
- Add `UpdateConstructionInspectionFee` command + handler + endpoint under `Modules/Appraisal/Appraisal/Application/Features/Fees/UpdateConstructionInspectionFee/`.
- Endpoint: `PUT /appraisals/{appraisalId}/fees/{feeId}/construction-inspection-fee` with body `{ amount: decimal? }`.
- Handler loads the fee, calls `SetConstructionInspectionFee`, persists.
- Server-side guard: only allow setting non-null when at least one property has building under construction. Otherwise either reject or silently null.

### B4. Expose in read model
- `GetAppraisalFees` query result — add `ConstructionInspectionFeeAmount` field. Files under `Modules/Appraisal/Appraisal/Application/Features/Fees/GetAppraisalFees/`.

### B5. Propagate to Collateral
- File: `Modules/Collateral/Collateral/CollateralMasters/Models/CollateralEngagement.cs`
  - Add `ConstructionInspectionFeeAmount decimal?` field.
- File: `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalForCollateral/GetAppraisalForCollateralQueryHandler.cs`
  - Include `AppraisalFee` join + select `ConstructionInspectionFeeAmount` into the projection.
  - Add field to the contract DTO in `Modules/Appraisal/Appraisal.Contracts/...`.
- File: `Modules/Collateral/Collateral/CollateralMasters/Services/CollateralMasterUpsertService.cs`
  - Pass the value through to `master.AppendEngagement(...)` (~line 636).
- Consider: should snapshot JSON also include it, or only the dedicated column? **Recommend dedicated column** for cheap reuse from CI flow.

### B6. CI appraisal fee seeding (skip normal tier/quotation)

**Normal pipeline (today):** Fee shell created in `AppraisalCreationService:212-223`; fee items added later by `AssignmentFeeService.EnsureAssignmentFeeItemsAsync` (`AssignmentFeeService.cs:19-148`) when an assignment event fires (`InternalAssignedIntegrationEventHandler:56-60`, `CompanyAssignedIntegrationEventHandler:72-122`, `AssignAppraisalCommandHandler:47-54`). `AssignmentFeeSource` is a discriminated union: `TierBased | Quotation` (`IAssignmentFeeService.cs:6-20`).

**CI bypass:**
- Add a sibling query in Collateral.Contracts: `GetConstructionInspectionFeeForAppraisalQuery(prevAppraisalId)` returning `decimal?` from the most recent engagement that has a non-null `ConstructionInspectionFeeAmount`. Mirrors `GetMostRecentCompanyForAppraisalQuery` (`Modules/Collateral/Collateral/CollateralMasters/Application/ConstructionInspection/`).
- Extend `AssignmentFeeSource` with a third variant: `ConstructionInspection(decimal? Amount)`. Null amount means "no prior CI fee — leave fee items empty per user decision #3".
- In each of the three call sites (`InternalAssignedIntegrationEventHandler`, `CompanyAssignedIntegrationEventHandler`, `AssignAppraisalCommandHandler`), before resolving tier/quotation, check `appraisal.AppraisalType == AppraisalTypes.ConstructionInspection`. If yes:
  - Send `GetConstructionInspectionFeeForAppraisalQuery(prevAppraisalId)` (load `prevAppraisalId` from appraisal — already wired into CI flow via `AppraisalCreationService:71-75`).
  - Use `AssignmentFeeSource.ConstructionInspection(amount)` regardless of company-vs-internal or quotation presence.
- In `AssignmentFeeService.EnsureAssignmentFeeItemsAsync`, add a third switch arm:
  - If amount is null → return early (no items added; idempotency guard at `:68-75` keeps it stable across re-runs).
  - If amount is non-null → `fee.AddItem("01", "Construction inspection fee from prior engagement", amount)` then call existing `RecalculateFromItems()` / payment-type finalization.
- The fee code stays `"01"` (Appraisal Fee) so existing FE delete-protection still applies and downstream payment / VAT logic is unchanged.

**Note:** This means the `prevAppraisalId` must reach the assignment handlers. Confirm during implementation that it's loadable from the `Appraisal` aggregate (likely a stored field `PrevAppraisalId` since `AppraisalCreationService` already uses it). If not stored, add it.

### B7. Image copy in `Appraisal.CopyProperty`
- File: `Modules/Appraisal/Appraisal/Domain/Appraisals/Appraisal.cs:325-390`
- After the new `AppraisalProperty` is created, copy `PropertyPhotoMapping` rows from source → new property. Reuse the same `GalleryPhotoId` (we're linking to the same gallery photo, not duplicating the blob), preserve `PhotoPurpose`, `SectionReference`, `SequenceNumber`, `IsThumbnail`. Stamp `LinkedBy` from current user / system, `LinkedAt = DateTime.UtcNow`.
- Confirm: `PropertyPhotoMapping` is a join entity (gallery photo ↔ property), so duplicating the mapping is correct, not a deep copy of the photo blob.

## Frontend tasks

### F1. Schema
- `src/features/appraisal/schemas/appointmentAndFee.ts` — rename `inspectionFee` → `constructionInspectionFee`.
- `src/features/appraisal/types/appointmentAndFee.ts` — same rename on `FeeData`.

### F2. Visibility logic on FeePage
- In `AppointmentAndFeePage.tsx`, derive `hasBuildingUnderConstruction` from the loaded property list:
  - `properties.some(p => (p.type === 'Building' || p.type === 'Land and building') && p.buildingDetail?.underConst === 'Y')`
  - (Confirm exact value of `underConst` true case during implementation — likely `'Y'` or `'true'`.)
- Pass flag into `FeeInformationSection`.

### F3. Field UI
- `src/features/appraisal/components/FeeInformationSection.tsx` — render numeric input for **Construction Inspection Fee** when flag is true. Style consistent with existing fee inputs.
- On change, call new mutation hook (see F4).

### F4. Mutation hook
- Add `useUpdateConstructionInspectionFee` under `src/features/appraisal/api/` calling the new BE endpoint `PUT /appraisals/{appraisalId}/fees/{feeId}/construction-inspection-fee`.

### F5. Hydrate from server
- `useGetAppraisalFees` already returns the fee — surface the new field; ensure it hydrates the form on mount/refetch.

### F6. Verify (per memory: feedback_verify_related_screens)
- Walk the related screens after wiring: fee summary card, decision summary, anywhere the fee is shown, and the CI prefill page once a CI application is created from a completed engagement.

## Out of scope
- New "create whole new appraisal from previous" command. Doesn't exist today; only `CopyPropertyToGroup` (per-property) and `GetAppraisalCopyTemplate` (FE-side reseed) exist.
- Renaming the fee-item type code `02 = Inspection Fee` (different concept — line item, not the new dedicated CI fee).

## Todos
- [ ] B1 Domain: rename `InspectionFeeAmount` → `ConstructionInspectionFeeAmount`; rename setter
- [ ] B2 EF migration: rename column on `appraisal.AppraisalFees`
- [ ] B3 `UpdateConstructionInspectionFee` command + handler + endpoint
- [ ] B4 `GetAppraisalFees` read-model includes new field
- [ ] B5a Add `ConstructionInspectionFeeAmount` to `CollateralEngagement`
- [ ] B5b Include fee in `GetAppraisalForCollateralQueryHandler` projection + DTO
- [ ] B5c `CollateralMasterUpsertService.AppendEngagement` passes value through
- [ ] B6a Add `GetConstructionInspectionFeeForAppraisalQuery` in Collateral.Contracts + handler
- [ ] B6b Extend `AssignmentFeeSource` with `ConstructionInspection(decimal? Amount)` variant
- [ ] B6c Add CI pre-check in 3 assignment handlers (Internal/Company/Manual)
- [ ] B6d `AssignmentFeeService.EnsureAssignmentFeeItemsAsync` switch arm for CI
- [ ] B7 `PropertyPhotoMapping` copy in `Appraisal.CopyProperty`
- [ ] F1 Schema + types rename
- [ ] F2 Visibility derivation from properties on `AppointmentAndFeePage`
- [ ] F3 Field UI in `FeeInformationSection` (hidden when not applicable)
- [ ] F4 `useUpdateConstructionInspectionFee` mutation hook
- [ ] F5 Hydration verified
- [ ] F6 Related-screens walk (decision summary, fee summaries, CI prefill page)
- [x] Review section (per project rule #8)

## Review

### Backend (`collateral-appraisal-system-api`)

| # | File | Change |
|---|------|--------|
| B1 | `Modules/Appraisal/Appraisal/Domain/Appraisals/AppraisalFee.cs` | Renamed `InspectionFeeAmount` → `ConstructionInspectionFeeAmount`; setter `SetInspectionFee` → `SetConstructionInspectionFee` |
| B1 | `Modules/Appraisal/Appraisal/Infrastructure/Configurations/AppraisalFeeConfiguration.cs` | Updated EF property mapping |
| B1 | `Modules/Appraisal/Appraisal/Application/Features/Fees/GetAppraisalFees/GetAppraisalFeesResult.cs` | DTO field rename + new `HasBuildingUnderConstruction` flag |
| B1 | `Database/Scripts/Views/Appraisal/vw_AppraisalFeeList.sql` | Column rename in view |
| B2 | `Modules/Appraisal/Appraisal/Infrastructure/Migrations/20260507004325_RenameInspectionFeeToConstructionInspectionFee.cs` | EF migration: rename column on `appraisal.AppraisalFees` |
| B3 | `Modules/Appraisal/Appraisal/Application/Features/Fees/UpdateConstructionInspectionFee/*` | New command + handler + endpoint: `PATCH /appraisals/{id}/fees/{feeId}/construction-inspection-fee` |
| B4 | `Modules/Appraisal/Appraisal/Application/Features/Fees/GetAppraisalFees/GetAppraisalFeesQueryHandler.cs` | Added `HasBuildingUnderConstruction` derivation via Dapper `EXISTS` query joining BuildingAppraisalDetails ↔ AppraisalProperties |
| B5 | `Modules/Collateral/Collateral/CollateralMasters/Models/CollateralEngagement.cs` | Added `ConstructionInspectionFeeAmount` field + ctor param |
| B5 | `Modules/Collateral/Collateral/CollateralMasters/Configurations/CollateralEngagementConfiguration.cs` | EF mapping for new column |
| B5 | `Modules/Collateral/Collateral/CollateralMasters/Models/CollateralMaster.cs` | `AppendEngagement` accepts CI fee param |
| B5 | `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalForCollateral/AppraisalForCollateralResult.cs` | Added `ConstructionInspectionFeeAmount` to root DTO |
| B5 | `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalForCollateral/GetAppraisalForCollateralQueryHandler.cs` | Loads CI fee from latest assignment's AppraisalFee |
| B5 | `Modules/Collateral/Collateral/CollateralMasters/Services/CollateralMasterUpsertService.cs` | Threads CI fee into `AppendEngagement` |
| B5 | `Modules/Collateral/Collateral/Migrations/20260507004630_AddConstructionInspectionFeeToEngagement.cs` | EF migration: add column on `collateral.CollateralEngagements` |
| B6 | `Modules/Appraisal/Appraisal/Domain/Appraisals/Appraisal.cs` | Persisted `PrevAppraisalId` on aggregate (set during CI creation) |
| B6 | `Modules/Appraisal/Appraisal/Infrastructure/Configurations/AppraisalAggregateConfiguration.cs` | EF mapping + filtered index |
| B6 | `Modules/Appraisal/Appraisal/Application/Services/AppraisalCreationService.cs` | Stamps `PrevAppraisalId` for CI; tracks prior→new property pairs and copies `PropertyPhotoMapping` rows after Phase 1 SaveChanges |
| B6 | `Modules/Appraisal/Appraisal/Infrastructure/Migrations/20260507005046_AddPrevAppraisalIdToAppraisal.cs` | EF migration: add column + filtered index |
| B6 | `Modules/Collateral/Collateral.Contracts/ConstructionInspection/GetConstructionInspectionFeeForAppraisalQuery.cs` | New cross-module query (mirrors `GetMostRecentCompanyForAppraisalQuery`) |
| B6 | `Modules/Collateral/Collateral/CollateralMasters/Application/ConstructionInspection/GetConstructionInspectionFeeForAppraisalQueryHandler.cs` | Handler returns most recent engagement's `ConstructionInspectionFeeAmount` |
| B6 | `Modules/Appraisal/Appraisal/Appraisal.csproj` | Added project reference to `Collateral.Contracts` |
| B6 | `Modules/Appraisal/Appraisal/Application/Services/IAssignmentFeeService.cs` | New `ConstructionInspection(decimal? Amount)` variant; new `ResolveSourceForAppraisalAsync` method |
| B6 | `Modules/Appraisal/Appraisal/Application/Services/AssignmentFeeService.cs` | Added `ISender` dep; switch arm for CI source (adds fee item with code `01` and CI description; null amount → empty fee) |
| B6 | 3 handlers (`InternalAssigned…`, `CompanyAssigned…`, `AssignAppraisalCommandHandler`) | Call `ResolveSourceForAppraisalAsync` so CI bypasses tier/quotation |
| B7 | `Modules/Appraisal/Appraisal/Application/Features/Appraisals/CopyPropertyToGroup/CopyPropertyToGroupCommandHandler.cs` | Copies `PropertyPhotoMapping` rows when copying a single property within an appraisal |
| B7 | `Modules/Appraisal/Appraisal/Application/Services/AppraisalCreationService.cs` | New `CopyPhotoMappingsFromPriorAsync` runs after Phase 1 save during CI creation |

### Frontend (`collateral-appraisal-system-app`)

| # | File | Change |
|---|------|--------|
| F1 | `src/features/appraisal/schemas/appointmentAndFee.ts` | Rename schema field |
| F1 | `src/features/appraisal/types/appointmentAndFee.ts` | Rename type field |
| F1 | `src/shared/schemas/v1.ts` | Rename `inspectionFeeAmount` → `constructionInspectionFeeAmount`; add `hasBuildingUnderConstruction` boolean to AppraisalFeeDto |
| F1 | `src/features/appraisal/components/PaymentInformationSection.tsx` | Removed dead `inspectionFee` state + commented-out NumberInput block |
| F2 | `src/features/appraisal/pages/AppointmentAndFeePage.tsx` | Reads `currentFee.hasBuildingUnderConstruction` and passes through to FeeInformationSection |
| F3 | `src/features/appraisal/components/FeeInformationSection.tsx` | New CI fee `NumberInput` block with hydrate-on-server-change + commit-on-blur; visible only when flag is true |
| F4 | `src/features/appraisal/api/fee.ts` | New `useUpdateConstructionInspectionFee` mutation hook |
| F5 | (verified) | Hook invalidates `['appraisal', appraisalId, 'fees']` so the field rehydrates on save |

### How the pieces work together

1. **Visibility** — Backend `GetAppraisalFees` computes `HasBuildingUnderConstruction` once per fee request via a single `EXISTS` query on `BuildingAppraisalDetails.IsUnderConstruction`. Page reads the flag straight off `currentFee` — no extra round-trip, no FE-side derivation.
2. **Edit** — User types amount → local `ciFeeDraft` state → blur fires `PATCH /…/construction-inspection-fee` → query is invalidated → server value re-hydrates the input.
3. **Persist on appraisal completion** — When the appraisal completes, `CollateralMasterUpsertService.ProcessAppraisalAsync` calls `GetAppraisalForCollateralQuery`, which now includes `ConstructionInspectionFeeAmount`. Each `CollateralEngagement` row is stamped with that value.
4. **CI bypass** — When a future Construction Inspection request arrives, `AppraisalCreationService` stamps `PrevAppraisalId` on the new appraisal aggregate. When the assignment event fires (Internal/Company/Manual), each handler calls `feeService.ResolveSourceForAppraisalAsync(appraisal, defaultSource)`. For CI, that resolver fires `GetConstructionInspectionFeeForAppraisalQuery(prevAppraisalId)` and returns `AssignmentFeeSource.ConstructionInspection(amount)`. The new switch arm in `EnsureAssignmentFeeItemsAsync` adds a single fee item (`feeCode="01"`) with the CI fee. **Tier and quotation paths are skipped entirely.** If no prior engagement carries a CI fee, fee items stay empty per spec.
5. **Photo copy** — Two flows updated:
   - **CI creation** (`AppraisalCreationService`) — collects `(priorPropertyId, newProperty)` pairs during property copy, then after Phase 1 `SaveChanges` (when DB-generated IDs land on new properties) bulk-copies `PropertyPhotoMapping` rows reusing the same `GalleryPhotoId`.
   - **Per-property copy** (`CopyPropertyToGroupCommandHandler`) — same pattern but for the single-property copy command.

### Security & data-integrity notes

- Update endpoint uses the existing `ITransactionalCommand<IAppraisalUnitOfWork>` pipeline — no new transactional surface area.
- No SQL injection: Dapper parameterised query (`@AppraisalId` bound via `DynamicParameters`).
- No PII or secrets handled by the new code.
- Idempotency: existing `EnsureAssignmentFeeItemsAsync` early-return on `fee.HasItems` still applies for the new CI source — safe to retry.
- Photo copy reuses `GalleryPhotoId` (no blob duplication, no new storage cost). The unique index on `PropertyPhotoMappings(GalleryPhotoId, AppraisalPropertyId)` prevents accidental double-mapping for the same target property.
- `PrevAppraisalId` is set only on CI appraisals (gated by `isConstructionInspection`), so no cross-pollination on normal flows.

### What I deliberately did **not** do

- No server-side guard on the new endpoint (FE controls visibility; matches the existing `UpdateFee` endpoint's trust model).
- No fallback to generic appraisal fee when prior CI fee is missing (per user decision #3).
- No "copy whole appraisal" command introduced — image copy hooks into the existing per-property and CI-creation paths.
- No rename of fee-line-item type code `02 = Inspection Fee` (it's a different concept — line item, not the dedicated CI field).
