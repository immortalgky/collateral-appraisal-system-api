# WQS Appraisal Price Unification

Consolidate the duplicate appraisal price fields. Store only what the user typed —
everything else is computed live on screen.

---

## Core principle

> **Store only user-edited values. Compute raw/derived values on screen.**

No "Rounded" suffix needed — the stored field IS the user's value.
No raw counterpart stored alongside it.

---

## Business rules (all cases)

| Case | `includeLandArea` | `hasBuildingCost` | Screen computes raw from |
|---|---|---|---|
| Land (unit 01) | true | false | `finalValueAdjusted × area` (Sq.Wa) |
| Condo (unit 02) | true | false | `finalValueAdjusted × area` (Sq.m) |
| Machinery / total (unit 03) | false | false | `finalValue` directly |
| Land + Building (unit 01) | true | true | `finalValueAdjusted × area` + `buildingCost` |

For **`hasBuildingCost = true`**: the full land section stays visible and `landValue`
is independently editable before combining with `buildingCost`.

---

## Current state → target state

### Backend `PricingFinalValues` columns

| Old column | Action | New column |
|---|---|---|
| `AppraisalPriceRounded` | RENAME | `LandValue` |
| `AppraisalPriceWithBuildingRounded` | RENAME | `AppraisalPrice` |
| `AppraisalPrice` | **DROP** | _(compute on screen)_ |
| `AppraisalPriceWithBuilding` | **DROP** | _(compute on screen)_ |
| `PriceDifferentiate` | **DROP** | _(compute on screen)_ |
| `BuildingCost` | unchanged | `BuildingCost` |

### Stored fields after change

```
LandValue      — user-editable land price
BuildingCost   — user-editable building cost
AppraisalPrice — user-editable final total (hasBuildingCost only)
```

### Computed on screen (never stored)

```
rawLandPrice    = finalValueAdjusted × area  (or finalValue for unit 03)
totalPrice      = LandValue + BuildingCost
landDiff        = rawLandPrice − LandValue
appraisalDiff   = totalPrice − AppraisalPrice
```

---

## Screen layout reference

```
!buildingCost, unit=01/02:
  Coefficient of decision  1
  Area                     4,000       Sq.Wa
  Final Value (editable)   [90,000 ▼]  Baht/Sq.Wa   ← finalValueAdjusted
  Appraisal Price          360,000,000 Baht           ← computed: finalValueAdjusted × area
  Appraisal Price (input)  [360M ▼]    Baht ±diff    ← LandValue (stored)
  Include building cost    [No] [Yes]

!buildingCost, unit=03 / Machinery:
  Coefficient of decision  1
  Final Value (editable)   [90,000 ▼]  Baht           ← finalValueAdjusted
  Appraisal Price          90,000      Baht            ← computed: finalValue
  Appraisal Price (input)  [90,000 ▼]  Baht ±diff    ← LandValue (stored)
  Include building cost    [No] [Yes]

buildingCost:
  Coefficient of decision  1
  Include building cost    [No] [Yes]
  --- land section ---
  Area                     4,000       Sq.Wa
  Price/Sq.Wa              90,000      Baht/Sq.Wa     ← finalValueAdjusted (display)
  Land Price (input)       [360M ▼]    Baht           ← LandValue (stored, editable)
  --- building section ---
  [BuildingCostTable]
  + Building Cost          40,000,000  Baht            ← BuildingCost (stored)
  Appraisal Price          400,000,000 Baht            ← computed: LandValue + BuildingCost
  Appraisal Price (input)  [400M ▼]    Baht ±diff    ← AppraisalPrice (stored)
```

---

## Todo

### Backend — `collateral-appraisal-system-api`

- [ ] **Migration** — rewrite `20260506200000_RenameLandValueAndDropBuildingPriceFields.cs`
  ```csharp
  // Up:
  RenameColumn("AppraisalPriceRounded"            → "LandValue")
  RenameColumn("AppraisalPriceWithBuildingRounded" → "AppraisalPrice")
  DropColumn("AppraisalPrice")          // old raw land
  DropColumn("AppraisalPriceWithBuilding")
  DropColumn("PriceDifferentiate")

  // Down:
  AddColumn("PriceDifferentiate", decimal, nullable)
  AddColumn("AppraisalPriceWithBuilding", decimal, nullable)
  AddColumn("AppraisalPrice", decimal, nullable)   // restore raw land
  RenameColumn("AppraisalPrice"  → "AppraisalPriceWithBuildingRounded")
  RenameColumn("LandValue"       → "AppraisalPriceRounded")
  ```

- [ ] **Domain** `Modules/Appraisal/Appraisal/Domain/Appraisals/PricingFinalValue.cs`
  - Remove properties: `AppraisalPrice` (raw), `AppraisalPriceWithBuilding`, `PriceDifferentiate`
  - Rename `AppraisalPriceRounded` → `LandValue`
  - Rename `AppraisalPriceWithBuildingRounded` → `AppraisalPrice`
  - Update `SetLandAreaValues(landArea, landValue)` — drop `appraisalPrice`/`priceDiff` params
  - Update `SetBuildingCost(buildingCost, appraisalPrice)` — only sets `BuildingCost` + `AppraisalPrice`
  - Update `ExcludeLandArea()` — clear `LandValue` only
  - Update `ClearBuildingCost()` — clear `BuildingCost` + `AppraisalPrice`

- [ ] **EF Config** `Modules/Appraisal/Appraisal/Infrastructure/Configurations/PricingConfiguration.cs`
  - `PricingFinalValueConfiguration`:
    - `f.AppraisalPriceRounded` → `f.LandValue`
    - `f.AppraisalPriceWithBuildingRounded` → `f.AppraisalPrice`
    - Remove entries for `AppraisalPrice` (raw), `AppraisalPriceWithBuilding`, `PriceDifferentiate`

- [ ] **Snapshot** `Modules/Appraisal/Appraisal/Infrastructure/Migrations/AppraisalDbContextModelSnapshot.cs`
  - `PricingFinalValues` entity block: apply same renames/drops as config

- [ ] **GetComparativeFactors result/response**
  `GetComparativeFactorsResult.cs` + `GetComparativeFactorsResponse.cs`
  - Update `FinalValueDto`:
    ```csharp
    record FinalValueDto(
      Guid Id,
      decimal FinalValue,
      decimal FinalValueRounded,
      decimal? FinalValueAdjusted,
      bool IncludeLandArea,
      decimal? LandArea,
      decimal? LandValue,       // user-editable land price
      decimal? BuildingCost,    // user-editable building cost
      decimal? AppraisalPrice,  // user-editable final total
      bool HasBuildingCost
    );
    ```

- [ ] **GetComparativeFactors query handler** `GetComparativeFactorsQueryHandler.cs`
  - Map `fv.LandValue`, `fv.BuildingCost`, `fv.AppraisalPrice`

- [ ] **UpdateFinalValue command** `UpdateFinalValueCommand.cs`
  - Keep: `IncludeLandArea`, `LandArea`, `LandValue`, `HasBuildingCost`, `BuildingCost`,
    `AppraisalPrice`
  - Remove: old `AppraisalPrice` (raw), `AppraisalPriceRounded`, `AppraisalPriceWithBuilding`,
    `AppraisalPriceWithBuildingRounded`, `PriceDifferentiate`

- [ ] **UpdateFinalValue request** `UpdateFinalValueRequest.cs` — same as command

- [ ] **UpdateFinalValue handler** `UpdateFinalValueCommandHandler.cs`
  - `SetLandAreaValues(command.LandArea, command.LandValue)`
  - `SetBuildingCost(command.BuildingCost, command.AppraisalPrice)`
  - Update result construction

- [ ] **UpdateFinalValue result/response** `UpdateFinalValueResult.cs` + `UpdateFinalValueResponse.cs`
  - Fields: `FinalValueId`, `FinalValue`, `FinalValueRounded`, `IncludeLandArea`, `LandArea`,
    `LandValue`, `HasBuildingCost`, `BuildingCost`, `AppraisalPrice`

- [ ] **Build check** `dotnet build Modules/Appraisal/Appraisal/Appraisal.csproj --no-restore -v q` → 0 errors

---

### Frontend — `collateral-appraisal-system-app`

- [ ] **Type** `src/features/pricingAnalysis/types/wqs.ts`
  - `WQSFinalValue` — replace building-duplicate fields:
    ```ts
    // remove:
    appraisalPrice: number
    appraisalPriceRounded: number
    totalBuildingCost?: number
    appraisalPriceIncludeBuildingCost?: number
    appraisalPriceIncludeBuildingCostRounded?: number
    priceIncludeBuildingCostDifferentiate?: number

    // add:
    landValue?: number       // stored user input (land)
    buildingCost?: number    // stored user input (building cost)
    appraisalPrice?: number  // stored user input (final total, hasBuildingCost only)
    ```

- [ ] **Schema** `src/features/pricingAnalysis/schemas/wqsForm.ts`
  - Keep `landValue` required; add optional `appraisalPrice`; remove old building duplicates

- [ ] **Field paths** `src/features/pricingAnalysis/adapters/wqsFieldPath.ts`
  - Add: `landValue`, `buildingCost`, `appraisalPrice`
  - Remove: `appraisalPriceRounded`, `appraisalPriceIncludeBuildingCost*`,
    `totalBuildingCost`, `priceIncludeBuildingCostDifferentiate`

- [ ] **Derived rules** `src/features/pricingAnalysis/adapters/buildWQSDerivedRules.ts`
  Add to `buildWQSFinalValueDerivedRules`:
  - `rawLandPrice` (display-only, not stored):
    - `includeLandArea`: `finalValueAdjusted × area`
    - `!includeLandArea`: `finalValue`
  - `landValue`: seed from `rawLandPrice` when empty (user can override)
  - `rawTotalPrice` (display-only, `hasBuildingCost`): `landValue + buildingCost`
  - `landDiff` (display-only): `rawLandPrice − landValue`
  - `appraisalDiff` (display-only, `hasBuildingCost`): `rawTotalPrice − appraisalPrice`
  - Remove old rules for `appraisalPriceIncludeBuildingCost*` / `totalBuildingCost`

- [ ] **Restore from saved data** `src/features/pricingAnalysis/adapters/restoreWQSFromSavedData.ts`
  - Map `landValue`, `buildingCost`, `appraisalPrice` from backend response
  - Remove old building-specific mappings

- [ ] **Initialize form** `src/features/pricingAnalysis/adapters/initializeWQSForm.ts`
  - Seed `landValue` from `finalValueRounded` when empty
  - Remove old building-specific seeds

- [ ] **Map to submit** `src/features/pricingAnalysis/domain/mapWQSFormToSubmitSchema.ts`
  - Send: `landValue`, `buildingCost`, `appraisalPrice`
  - Remove all old building-duplicate fields

- [ ] **UI** `src/features/pricingAnalysis/components/WQSAdjustFinalValueSection.tsx`
  - `!hasBuildingCost`: "Appraisal Price" row = computed raw; "Appraisal Price (input)" = `landValue`
  - `hasBuildingCost`:
    - Land section: Area, Price/Sq.Wa display, "Land Price (input)" = `landValue` (editable)
    - Building section: BuildingCostTable, +BuildingCost, "Appraisal Price" = computed
      `landValue + buildingCost`, "Appraisal Price (input)" = `appraisalPrice` (editable), ±diff

---

## Review

_To be filled after implementation._
