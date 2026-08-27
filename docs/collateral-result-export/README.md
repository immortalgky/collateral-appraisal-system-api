# Outbound "Collateral Result" interface (CAS → AS400)

Ships a fixed-width **231-char** H/D/T file of completed appraisals back to the host (AS400) so it can
update collateral prices. Runs on a Hangfire recurring job **once daily** and is driven by a
**sent-ledger** (`collateral.CollateralResultLogs`) so an appraisal that completes *after* a run is
picked up by the next one.

**The row set is the appraisals, not the collateral masters.** It used to start from
`CollateralEngagements` joined to `CollateralMasters` with `HostCollateralId IS NOT NULL` as the
eligibility gate, which made the file a function of whether a master had been created — and 6,699
completed appraisals on the production-like dataset never get one (a condo with no sub-district, land
with no title number, a leasehold that will not resolve). Those appraisals were silently absent from
every file. Starting from the appraisals means a missing master cannot hide one.

## Finding the AS400 collateral id

AS400 stamps our appraisal number into CCSURV when it mints a collateral id, and only moves it on a
**new drawdown**. A reappraisal involves no drawdown, so the id keeps pointing at the older
appraisal — **91.3% of held master-title rows name the head of their chain**, not the latest round.

So each completed appraisal walks **up** `PrevAppraisalId` and stops at the first ancestor AS400
knows. The nearer ancestor wins: it reflects the more recent drawdown. The walk finds the **id only**
— every value in the record comes from the appraisal that was actually completed.

| What the walk lands on | What goes out |
|---|---|
| exactly one collateral | that id, `Auto Update = Y` |
| several, appraisal is a block project | one row per matched unit, each `Auto Update = Y` |
| several, not a block project | blank id, `Auto Update = N` |
| nothing | blank id, `Auto Update = N` |

A blank id still identifies the work — the appraisal number is in the record — and `Auto Update = N`
tells the host a person needs to look at it. Guessing which of several collateral a price belongs to
would update the wrong one.

**Block projects are the exception that resolves.** AS400 mints an id per financed unit and writes
the unit key into `CollateralName` as `CONDO.<room> <deeds>`; our own units carry the same room
number. Matching on it produces one row per unit, each with its own price and its own land area —
not one price repeated.

The sent-ledger is keyed on **`(AppraisalId, CollateralId)`**. Keying on the appraisal alone
structurally forbade the per-unit case, and it also meant an appraisal sent before AS400 minted its
id was marked done forever; it now gets another turn once the id exists.

## Where things live

| Piece | Path |
|---|---|
| 231-char writer | `Modules/Integration/Integration/FileInterface/Format/CollateralResult/CollateralResultFileWriter.cs` |
| Export query | `Modules/Integration/Integration/FileInterface/Format/CollateralResult/CollateralResultExportQuery.cs` |
| Row set + id resolution | `Database/Scripts/Views/Collateral/vw_CollateralResultExport.sql` |
| Dry run (build without sending) | `GET /file-interfaces/admin/collateral-result/preview` |
| Row contract | `Modules/Collateral/Collateral.Contracts/FileInterface/ICollateralResultQuery.cs` |
| Hangfire job | `Modules/Integration/Integration/FileInterface/Jobs/CollateralResult/CollateralResultExportJob.cs` |
| Sent-ledger entity | `Modules/Collateral/Collateral/CollateralMasters/Models/CollateralResultLog.cs` |
| Transport port | `Modules/Integration/Integration.Contracts/FileSink/IOutboundFileSink.cs` |
| Transport impls (Local/Sftp) | `Modules/Integration/Integration/Infrastructure/FileSink/` |
| Recurring registration | `Modules/Integration/Integration/Scheduling/IntegrationRecurringJobs.cs` → job id `collateral-result-export` (daily at 00:00 local) |

Enrichment captured onto `CollateralEngagement` at appraisal completion: `ForcedSaleValue`,
`InternalAppraiserName` (+ `MachineDetail.LifeYear`), sourced via `Appraisal.Contracts`
`GetAppraisalForCollateralQuery`.

## 231-char Detail layout

Authoritative source: `CollateralResultFileWriter.DetailFields`. Widths sum to exactly 231.

The vendor spec reserves a decimal point in every scale-2 field; we send **implied decimals** instead
(value ×100, no dot), so each `decimal(15,2)` occupies 15 chars rather than 16. That is why positions
here run 10 chars shorter than the spec's own numbering from `Appraisal Value` onward. AS400 confirmed
the implied-decimal layout in the 2026-08 revision, which numbers the fields exactly as below.

| Pos | Field | Width | Source |
|---|---|---|---|
| 1 | Record Type (`D`) | 1 | const |
| 2-20 | Collateral ID (host) | 19 | `collateral.HostCollateralLinks.HostCollateralId`, found by the chain walk above |
| 21-30 | Appraisal Report No | 10 | `appraisal.Appraisals.AppraisalNumber` |
| 31-45 | Appraisal Value | 15 | `appraisal.ValuationAnalyses.AppraisedValue`; a block unit uses its own `ProjectUnitPrices.TotalAppraisalValueRounded` |
| 46-60 | Land Value | 15 | cost approach only: `PricingFinalValues.FinalValueAdjusted × land area` |
| 61-75 | Building Value | 15 | cost approach only: `PricingFinalValues.BuildingValue` |
| 76-90 | Force Sale Value | 15 | `appraisal.ValuationAnalyses.ForcedSaleValue` |
| 91-98 | Current Appraisal Date | 8 | `appraisal.ValuationAnalyses.ValuationDate` `DDMMYYYY` |
| 99-106 | Next Appraisal Date | 8 | current + 3y |
| 107-110 | Internal Valuer Code | 4 | `auth.AspNetUsers.EmployeeId` of the latest assignment's appraiser — Internal path only, **see below** |
| 111-150 | Internal Valuer Name | 40 | `auth.AspNetUsers` name, falling back to `AppraisalAssignments.InternalAppraiserName` — Internal path only |
| 151-154 | External Valuer Code | 4 | `auth.Companies.HostCompanyCode` — External path only |
| 155-194 | External Valuer Name | 40 | `auth.Companies.Name` — External path only |
| 195-197 | Life Year | 3 | `appraisal.MachineCostItems.LifeSpanYears` (machinery only) |
| 198 | Appraisal Status | 1 | `A` approved / `R` rejected |
| 199-201 | Building Age (`CCEBIL`) | 3 | oldest building on the appraisal / condo — **see below** |
| 202-208 | Area Utilization (`CCEARE`) | 7 | total building area / condo usable area, sq.m — **see below** |
| 209 | Auto Update | 1 | `Y` only when the appraisal tied to exactly one collateral |
| 210-214 | Land Area — Rai | 5 | this row's collateral: the unit's own for a block, the appraisal's otherwise |
| 215-217 | Land Area — Ngan | 3 | |
| 218-221 | Land Area — Square Wa | 4 | implied dec(4,2) |
| 222-231 | Land Area total | 10 | the **whole appraisal** in square wa; identical on every row of a block project by design |

Alpha = left-justified and space-padded; numeric = right-justified and **zero**-filled, because AS400
zoned-decimal fields cannot hold spaces — a null or zero amount goes out as all zeros, not blanks.
Over-long alpha values are truncated; over-long numerics throw rather than corrupt a price.

### Building Age and Area Utilization

Added in the 2026-08 spec revision. Both are sourced by what the appraisal holds:

| Appraisal holds | Building Age | Area Utilization |
|---|---|---|
| a building (`B`, `LB`, `LSB`, `LS`) | `MAX(BuildingAge)` over `appraisal.BuildingAppraisalDetails` | `SUM(TotalBuildingArea)` over the same rows |
| a condo (`U`, `LSU`) | `CondoAppraisalDetails.BuildingAge` | `CondoAppraisalDetails.UsableArea` |
| bare land or machinery | zeros | zeros |

Land area is carried in its own fields (210-231) in Thai units, read straight from
`appraisal.LandTitles`, which stores rai / ngan / square wa separately.

Aggregating rather than taking the `Sequence = 1` building matters when a title carries a house plus an
outbuilding: the host needs the whole footprint, and the oldest structure drives its depreciation view.
`vw_RegulatoryExport` follows the identical rule — **change the two together**.

Bare land reports nothing here on purpose. The field means usable *floor* area in sq.m, whereas land
area is held in sq.wa; sending it would be silently wrong rather than merely missing.

Range guards mirror `LifeYear`: an age outside `[0, 999]` or an area over `99999.99` is dropped to
zeros rather than truncated, so one bad collateral cannot abort the nightly run. Rules live in
`CollateralResultQuery.ToBuildingAge` / `.ToAreaUtilization` (unit-tested).

> ⚠️ `CollateralEngagementBuildings.BuildingAge` only exists from migration `20260625022752` onward.
> Engagements created before 2026-06-25 have `NULL` and go out as `000`. No backfill is planned;
> the source values are still on the appraisal if one is ever wanted.

### Internal vs External valuer — one pair per record

An appraisal ran on the External path (an appraisal company produced the book) or the Internal path
(in-house appraiser), never both. Positions 107-150 and 151-194 are therefore **mutually exclusive**:
one pair carries data, the other goes out blank. `R` rows blank both.

The discriminator is `CollateralEngagements.AppraisalCompanyId IS NOT NULL ⇒ External` — the same rule
the rest of the system uses (`AppraisalAssignments.AssigneeCompanyId IS NOT NULL`, see
`vw_AppraisalDetail`), read off the value **frozen onto the engagement**. We deliberately do not join
back to `appraisal.AppraisalAssignments.AssignmentType`: the engagement is a point-in-time snapshot,
and the live assignment can have been reassigned since the file's appraisal completed.

Off-system engagements (the `EXTO` decision / `Offline` assignment method) count as **External**: the
company did the work even though a bank staffer keyed the book in.

Neither pair can be trusted to be null on its own, which is why the branch is explicit:

- On the External path the engagement still carries `AppraiserUserId` / `InternalAppraiserName` —
  the bank's follow-up officer, or the *company's own* appraiser when the assignment has no follow-up
  staff, because `GetAppraisalForCollateralQueryHandler` resolves them through an
  `AssigneeUserId ?? InternalAppraiserId ?? ExternalAppraiserId` chain that never consults
  `AssignmentType`.
- An off-system engagement carries both pairs outright.

Rule lives in `CollateralResultQuery.SelectValuerFields` / `.IsExternalEngagement` (unit-tested in
`Tests/Unit/Collateral.Tests/CollateralResult/ValuerPathSelectionTests.cs`). An external engagement
whose company row no longer resolves goes out with all four fields blank — better than a name on the
wrong side.

### Internal Valuer Code — the 4-character problem

`AppraiserUserId` holds a **username**, so the code is resolved by joining
`auth.AspNetUsers.UserName` — the same join that resolves the appraiser's display name.

The AS400 field is **4 characters while `EmployeeId` is 5**, almost always zero-padded. So:

| `EmployeeId` | Sent | Why |
|---|---|---|
| `06327` | `6327` | leading zeros stripped, fits |
| `123` | `123` | already fits |
| `81018` | *(blank)* + warning | 5 significant digits — cannot fit |
| absent | *(blank)* | no code on file |

An id that will not fit is **dropped, not shortened**: the writer truncates left-aligned fields
silently, and `81018` cut to `8101` would name a different member of staff in the core banking system.
One warning per export run lists the affected appraisers.

Rule lives in `CollateralResultQuery.ToInternalValuerCode` (unit-tested).
**Asking AS400 to widen this field to 5** would let every appraiser's code go out — recorded as an open
item in `.claude/tasks/as400-host-collateral-link.md`.
Header: `H` + EffectiveDate(`DDMMYYYY`) + filler. Trailer: `T` + 9-char right-aligned detail count + filler.
UTF-8 (no BOM), CRLF line endings. Detail-row grain = one row per appraisal (primary master).

## Configuration

`appsettings*.json`:

```json
"OutboundFileSink": {            // transport — owned by Integration module
  "FileSource": "Local",         // Local | Sftp
  "Local": { "Path": "./outbound" },
  "Sftp": { "Host": "", "Port": 22, "Username": "", "Password": "", "RemoteDirectory": "/outgoing" }
},
"CollateralResultExport": {      // export — owned by Collateral module
  "FileNamePrefix": "COLLATERAL_RESULT_"   // → COLLATERAL_RESULT_yyyyMMddHHmmss.txt
}
```

## Manual testing (dev)

1. Pick a **Completed** appraisal and set the host id on its engagement (simulates the inbound feed):
   ```sql
   UPDATE collateral.CollateralEngagements
   SET HostCollateralId = '25909'
   WHERE AppraisalId = '<appraisal-id>';
   ```
   (For a freshly completed appraisal, `ForcedSaleValue` / `InternalAppraiserName` / `LifeYear` are
   captured automatically on the engagement.)
2. Open `/hangfire` → **Recurring jobs** → `collateral-result-export` → **Trigger now**.
3. Check the output file in `./outbound/` (relative to the API working dir):
   ```bash
   awk '{ print length($0) }' outbound/COLLATERAL_RESULT_*.txt | sort -u   # must print 231, once
   cut -c107-150 outbound/COLLATERAL_RESULT_*.txt                          # internal valuer pair
   cut -c151-194 outbound/COLLATERAL_RESULT_*.txt                          # external valuer pair
   cut -c199-208 outbound/COLLATERAL_RESULT_*.txt                          # age + area
   cut -c209-231 outbound/COLLATERAL_RESULT_*.txt                          # auto-update + land area
   ```
   Exactly one valuer pair may be filled per `D` row: an engagement with `AppraisalCompanyId` set must
   show blanks at 107-150, one without it must show blanks at 151-194. Cover both by exporting an
   internal and an external appraisal in the same run.
   The trailer count must equal the number of `D` records.
4. Check the ledger: `SELECT * FROM collateral.CollateralResultLogs;`. Trigger again → the same
   appraisal is **not** re-sent. Complete another appraisal (with a host id), trigger again → only the
   new one is emitted.
5. To exercise the new fields, cover three shapes: an L&B engagement with **two or more** buildings
   (area must equal the sum, age the max), a condo, and bare land (both fields all zeros).

## Migration

`Modules/Collateral/Collateral/Migrations/*_AddCollateralResultExport.cs` adds:
`CollateralMasters.HostCollateralId`, `CollateralEngagements.{ForcedSaleValue, InternalAppraiserName, LandValue, BuildingValue}`,
`MachineDetails.LifeYear`, and the `CollateralResultLogs` table.
**Apply it yourself** (`dotnet ef database update --context CollateralDbContext …`) — it is not applied automatically here.
