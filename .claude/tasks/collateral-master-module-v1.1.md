# Collateral Master v1.1 — Multi-Title + Normal-Workflow Appeal Exclusion

> v1 has shipped (45/45 backend tests + frontend integration). Two real gaps surfaced during testing:
> 1. The non-quotation "normal appraisal kickstart" workflow doesn't apply the appeal company exclusion (only `start-from-task` does).
> 2. A Land property can have multiple title deeds, but the master only captures the first one. Other titles are invisible to dedup/lookup.
>
> This plan addresses both. The v1 spec at `.claude/tasks/collateral-master-module-v1.md` (in the project repo) is preserved as the v1 reference.

---

## Context

**Why now:** without these fixes, two production scenarios fail:
- **Appeal via direct workflow** (no quotation): the bank can route an appeal directly through `CompanySelectionActivity`. Today it reads a singular `excludedCompanyId` from workflow variables — there's no way to exclude *multiple* prior companies.
- **Multi-parcel Land**: Thai practice routinely groups N adjacent parcels (each with its own title deed) as one collateral. Reappraising such a property today produces inconsistent dedup — only the first title is matched; the rest are ignored. A reappraisal with re-ordered titles can fail to find the existing master.

**Intended outcome:** v1.1 makes both work without changing the v1 read API surface or migration story for already-deployed environments.

---

## Issue 1 — Normal-workflow appeal exclusion

### Business rule (per user)

**Exclude only the MOST RECENT prior company** — not the full history. The lookup endpoint already exposes the most recent engagement at `lookupResult.lastEngagement.appraisalCompanyId`. We use that single ID.

This means **no signature changes** to `CompanySelectionActivity` or `ICompanyRoundRobinService` — they already support singular `excludedCompanyId`. The work is purely **wiring**: ensure the singular ID flows from the FE lookup result into the normal-workflow kickstart variables.

### Current state

| Path | Status |
|---|---|
| `POST /quotations/start-from-task` | ✓ Accepts `ExcludedCompanyIds` (kept as a list for backwards compat with v1 — FE sends a 0-or-1-element list) |
| `CompanySelectionActivity` (workflow) | ✓ Already reads singular `excludedCompanyId` (`Modules/Workflow/Workflow/Workflow/Activities/CompanySelectionActivity.cs:43`); no signature change needed |
| `ICompanyRoundRobinService.SelectCompanyAsync` | ✓ Already takes `Guid?`; no change |
| Workflow variable seeding for non-quotation kickstart | ✗ Doesn't set `excludedCompanyId` from the lookup result |
| FE → non-quotation kickstart command | ✗ Doesn't pass the most-recent prior company id |

### Changes

1. **Frontend `appealExclusionStore`**: refactor from `excludedCompanyIds: string[]` to `excludedCompanyId: string | null`. Source: `lookupResult.lastEngagement.appraisalCompanyId` (already in the v1 lookup response). When no prior engagement → `null`.

2. **Frontend non-quotation kickstart**: find where the request submission triggers the workflow start (search FE for the command that posts the workflow start variables). Add `excludedCompanyId` to the variable map, sourced from the store. If null, omit or send empty.

3. **Backend workflow start endpoint** (the one that kicks off a normal appraisal workflow — find via grep on the integration event or controller for "start workflow / start appraisal"): accept `excludedCompanyId` in the request body (optional Guid?) and pass it through to the workflow as the existing variable `excludedCompanyId`.

4. **Quotation `start-from-task`** (v1): no change. FE simply sends `excludedCompanyIds` as a 0- or 1-element list. Backend's existing 409-collision check remains.

### Critical files
- `Modules/Workflow/Workflow/Workflow/Activities/CompanySelectionActivity.cs` — **no change** (already singular)
- Backend kickstart-workflow endpoint (TBD — find via `excludedCompanyId` search)
- FE: `src/features/collateralMaster/store/appealExclusionStore.ts` — change shape to single id
- FE: non-quotation kickstart command (TBD — find via FE search for the workflow-start mutation)
- FE: `src/features/quotation/pages/NewQuotationPage.tsx` and `src/features/appraisal/components/CreateQuotationModal.tsx` — adjust to read the single id and wrap in single-element array for the v1 plural-list contract

### Why most-recent only

If a collateral has been appraised by Companies A → B → C (in chronological order), an appeal of the latest excludes only **C**. This matches the typical legal/business meaning of "appeal" — the customer is contesting the most recent value, not all historical values. Older companies (A, B) remain eligible to bid; in fact rotating back to them is sometimes preferred.

---

## Issue 2 — Multi-title via IsMaster pattern

### Design (per user decision)

- **Every title becomes its own `CollateralMaster` row.** No 1:N child table.
- One row per group has `IsMaster = true`; the others (`IsMaster = false`) are aliases.
- Aliases point at the master via `ParentMasterId` (self-FK on `CollateralMaster`).
- **Heavy data lives only on the IsMaster row**: last-known fields, construction tracking, `LastAppraisedValue`, `LastTotalAppraisedValue`, `LastConstructionInspectionId`, etc.
- Aliases carry only their own dedup-key columns + `ParentMasterId`.
- **Engagements attach to the IsMaster row** — never to aliases. One engagement per appraisal property even if it has 5 titles.
- Pattern applies uniformly to all types. Condo/Leasehold/Machine: every row is `IsMaster = true` (singleton group); `ParentMasterId = NULL`.

### Schema additions to `CollateralMasters` table

| Column | Type | Default | Purpose |
|---|---|---|---|
| `IsMaster` | bit | NO, default 1 | True = primary row carrying all the heavy data; False = title alias |
| `ParentMasterId` | uniqueidentifier | YES | Self-FK to `CollateralMasters.Id` (NULL when `IsMaster = 1`); RESTRICT delete |

For backfill: existing rows from v1 all get `IsMaster = 1, ParentMasterId = NULL` (singleton groups). No data movement.

### Detail tables

- `LandDetails`: row exists for every CollateralMaster row (master + aliases). On alias rows, last-known and value fields are nullable / NULL. Dedup key columns (LandOfficeCode, Province, Amphur, Tambon, TitleDeedType, TitleDeedNo, SurveyOrParcelNo) are populated for all rows.
- `CondoDetails` / `LeaseholdDetails` / `MachineDetails`: one row per CollateralMaster as today; pattern unchanged because they're singleton groups.

### Leasehold-over-multi-title Land — explicit handling

A leasehold has ONE ContractNo but its underlying Land may have many titles. The flow:

1. Land appraisal upsert produces a group: 1 Land IsMaster row + N-1 Land alias rows (per the rules above).
2. Leasehold upsert (Pass 2) resolves the underlying property by looking up Land by ANY of the leasehold's underlying titles. The hit may land on an alias — navigate via `ParentMasterId` to the Land IsMaster row.
3. Leasehold's `UnderlyingMasterId` is set to the Land IsMaster row's Id (never to an alias).
4. The Leasehold itself remains a singleton group (`IsMaster = 1, ParentMasterId = NULL`).
5. Reverse lookup ("leaseholds over this Land property") works via `WHERE UnderlyingMasterId = land.IsMaster.Id`.

This means: searching for the Land by any of its titles surfaces the master + the Leasehold attached to it transparently. No extra schema change for Leasehold — the IsMaster pattern on the Land side does the work.

### Filtered unique indexes

Stay as-is — each detail table's unique index keeps each title independently unique. The IsMaster status doesn't affect uniqueness; it just affects which row holds the heavy data.

### Catalog / lookup behavior

- **Catalog (admin browse)**: filter `WHERE IsMaster = 1` — one row per physical property (no double-counting aliases).
- **Lookup (dedup)**: search across ALL rows. If a hit lands on an alias, navigate to the master via `ParentMasterId` and return the master's data. Show all alias titles in the result so the user sees the full title set.
- **Construction-progress / engagements**: always go through the IsMaster row.

### Upsert behavior (`CollateralMasterUpsertService.UpsertLandAsync`)

For an appraisal with N titles:

```
1. For each title T in appraisal.LandTitles:
     - Look up by T's dedup key (LandOffice + Province + Amphur + Tambon + TitleDeedType + TitleDeedNo + SurveyOrParcelNo)
     - If found, resolve to its master (via ParentMasterId if alias)
     - Collect distinct master IDs in `matchedMasterIds`

2. Decide:
   a. matchedMasterIds is empty → create new group:
        - Create IsMaster row with the FIRST title (or admin-picked, or longest-area title — pick one rule)
        - Create alias rows for the other titles, ParentMasterId = master.Id
   b. matchedMasterIds has exactly 1 → reuse that master:
        - For each title not yet in this master's group, create an alias row
        - Mark removed titles (in master group but not in this appraisal) as `IsHistorical = false` for v1.1 — leave them as-is (preserve audit). v2 may add an explicit historical flag.
   c. matchedMasterIds has > 1 → conflict:
        - Throw a ConflictException listing the conflicting master IDs
        - Admin must resolve via merge (deferred to v2; not in this scope)

3. Append engagement to the IsMaster row only.
4. Update last-known fields + construction tracking on the IsMaster row only.
5. Compute LastTotalAppraisedValue on the IsMaster row (sum of land + buildings as today).
```

### Validation gate (Appraisal-side)

`Appraisal.Complete()` already requires ≥1 title with non-empty `TitleDeedNo` + `TitleDeedType`. **No change needed** — multi-title is already handled at the validation level.

### Engagement snapshot JSON

Add `titles[]` array to the Land snapshot so the full title set is captured per visit:
```json
{
  "type": "Land",
  "titles": [
    { "titleDeedNo": "12345", "titleDeedType": "Chanote", "surveyOrParcelNo": "..." },
    { "titleDeedNo": "12346", "titleDeedType": "Chanote", "surveyOrParcelNo": "..." }
  ],
  ...
}
```

### Critical files
- `Modules/Collateral/Collateral/CollateralMasters/Models/CollateralMaster.cs` — add `IsMaster`, `ParentMasterId` properties + factory updates
- `Modules/Collateral/Collateral/CollateralMasters/Configurations/CollateralMasterConfiguration.cs` — index + FK config
- `Modules/Collateral/Collateral/CollateralMasters/Services/CollateralMasterUpsertService.cs:200` — replace single-title pick with multi-title group logic
- `Modules/Collateral/Collateral/Application/Features/CollateralMasters/Lookup/LookupCollateralMasterQueryHandler.cs` — alias-aware: if hit is alias, navigate to master
- `Modules/Collateral/Collateral/Application/Features/CollateralMasters/GetCatalog/GetCollateralCatalogQueryHandler.cs` — filter `IsMaster = 1`
- `Database/Scripts/Views/Collateral/vw_CollateralMasters.sql` — filter `IsMaster = 1`
- `Database/Scripts/Views/Collateral/vw_CollateralEngagements.sql` — engagements already go through IsMaster only; verify
- `Modules/Collateral/Collateral/CollateralMasters/Services/SnapshotBuilder.cs` — emit `titles[]` array for Land

### Migration

One additive migration `AddIsMasterAndParentMasterId`:
- Add `IsMaster` bit NOT NULL DEFAULT 1
- Add `ParentMasterId` uniqueidentifier NULL with FK + RESTRICT delete
- Update existing rows: all v1 data becomes `IsMaster = 1` (already the default — no data movement needed)
- No backfill needed for the data itself; the master/alias relationship will be established naturally as new appraisals come in. Optionally a one-shot reconciliation script can group existing single-title masters that share LandOffice + Province + admin levels but had different title numbers (rare; out of scope unless requested).

---

## Verification

### Backend tests (extend `Tests/Integration/Collateral.Integration.Tests/`)

**Multi-title:**
- [ ] Land appraisal with 3 titles creates 1 IsMaster + 2 aliases. Engagement count = 1.
- [ ] Reappraisal of same 3 titles in same order: same group, no new aliases, engagement count = 2.
- [ ] Reappraisal with one title removed (now 2 of 3): same group, removed title's alias stays in DB, engagement count = 2.
- [ ] Reappraisal with one new title added (now 4): same group, new alias created, engagement count = 2.
- [ ] Lookup by any of the 3 titles returns the master with all aliases visible.
- [ ] Two existing masters that overlap on a new appraisal's titles → 409 Conflict.
- [ ] Catalog query returns 1 row per group (filtered IsMaster = 1).
- [ ] Condo/Leasehold/Machine appraisals create rows with IsMaster = 1 (singleton groups).

**Normal-workflow appeal exclusion (most-recent-only):**
- [ ] FE `appealExclusionStore` holds single `excludedCompanyId` sourced from `lookupResult.lastEngagement.appraisalCompanyId`.
- [ ] Non-quotation kickstart: workflow starts with the variable set; `CompanySelectionActivity` reads it (no activity change).
- [ ] When no prior engagement (lookup miss), no exclusion applied — kickstart proceeds normally.
- [ ] When the prior most-recent company is the one being picked → activity rejects with clear error (existing behavior).
- [ ] Quotation `start-from-task` continues to work with FE sending a 0-or-1-element `excludedCompanyIds[]`.

### Manual smoke

1. Create initial appraisal with 3 land titles → confirm 1 IsMaster + 2 aliases via DB.
2. Reappraise same property by entering only one of the titles in request creation → lookup finds master, banner shows all 3 titles.
3. Send a new request as appeal → kick off NORMAL appraisal workflow (not quotation). Confirm the company-selection activity respects the excluded list (try to pick a prior company → rejected).
4. Catalog admin page: verify the master shows once, not 3 times.

---

## Out of scope (v2)

- Master merge UI (when overlap conflict occurs, admin must currently fix data manually).
- Historical title flagging (titles that were on previous appraisals but removed from the latest — currently kept in DB without an explicit historical flag).
- Cross-master deduplication backfill (one-shot script to group existing v1 masters that should have been one group).
- Frontend display of alias titles in catalog/detail screens — v1.1 backend change is transparent to existing FE; the lookup endpoint will return the alias title set in its response shape, but the catalog/detail UI may need a follow-up to render multi-title cards. Document as v1.1.x follow-up.
