# Outbound "Collateral Result" interface (CAS → AS400)

Ships a fixed-width **202-char** H/D/T file of completed appraisals back to the host (AS400) so it can
update collateral prices. Runs on a Hangfire recurring job **once daily at midnight** and is driven by a
**sent-ledger** (`collateral.CollateralResultLogs`) so a collateral that completes *after* a run is
picked up by the next run (next midnight).

> ⚠️ **Dormant until host mapping exists.** Each row is keyed by `CollateralMaster.HostCollateralId`,
> which is populated by a *separate future inbound interface*. Until that runs, the export finds no
> eligible rows and writes nothing. All plumbing works today — seed a `HostCollateralId` manually to test.

## Where things live (self-contained in the Collateral module)

| Piece | Path |
|---|---|
| 202-char writer | `Modules/Collateral/Collateral/CollateralMasters/CollateralResult/CollateralResultFileWriter.cs` |
| Export query (collateral schema only) | `…/CollateralResult/CollateralResultQuery.cs` |
| Hangfire job | `…/CollateralResult/CollateralResultExportJob.cs` |
| Sent-ledger entity | `…/Models/CollateralResultLog.cs` |
| Transport port | `Modules/Integration/Integration.Contracts/FileSink/IOutboundFileSink.cs` |
| Transport impls (Local/Sftp) | `Modules/Integration/Integration/Infrastructure/FileSink/` |
| Recurring registration | `Bootstrapper/Api/Program.cs` → job id `collateral-result-export` (daily at 00:00 local) |

Enrichment captured onto `CollateralEngagement` at appraisal completion: `ForcedSaleValue`,
`InternalAppraiserName` (+ `MachineDetail.LifeYear`), sourced via `Appraisal.Contracts`
`GetAppraisalForCollateralQuery`.

## 202-char Detail layout

| Pos | Field | Width | Source |
|---|---|---|---|
| 1 | Record Type (`D`) | 1 | const |
| 2-20 | Collateral ID (host) | 19 | `CollateralMaster.HostCollateralId` |
| 21-30 | Appraisal Report No | 10 | `CollateralEngagement.AppraisalNumber` |
| 31-46 | Appraisal Value | 16 | `CollateralEngagement.AppraisalValue` |
| 47-62 | Land Value | 16 | `CollateralEngagement.LandValue` — frozen at completion (cost split `UnitPrice × LandArea`, Land/L&B only) |
| 63-78 | Building Value | 16 | `CollateralEngagement.BuildingValue` — frozen at completion (cost `BuildingCost`, L&B only) |
| 79-94 | Force Sale Value | 16 | `CollateralEngagement.ForcedSaleValue` |
| 95-102 | Current Appraisal Date | 8 | `CollateralEngagement.AppraisalDate` (appointment date) `DDMMYYYY` |
| 103-110 | Next Appraisal Date | 8 | current + 3y |
| 111-114 | Internal Valuer Code | 4 | **blank** (no AS400 code in CAS) |
| 115-154 | Internal Valuer Name | 40 | `CollateralEngagement.InternalAppraiserName` |
| 155-158 | External Valuer Code | 4 | **blank** |
| 159-198 | External Valuer Name | 40 | `CollateralEngagement.AppraisalCompanyName` |
| 199-201 | Life Year | 3 | `MachineDetail.LifeYear` (machinery only) |
| 202 | Appraisal Status | 1 | `A` (only Completed appraisals; `R` deferred) |

Alpha = left-justified, numeric = right-justified, space-padded; over-long values truncated.
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

1. Pick a **Completed** appraisal and set the host id on its primary master (simulates the future inbound):
   ```sql
   UPDATE collateral.CollateralMasters
   SET HostCollateralId = '25909'
   WHERE Id = '<primary-master-id>' AND IsMaster = 1;
   ```
   (For a freshly completed appraisal, `ForcedSaleValue` / `InternalAppraiserName` / `LifeYear` are
   captured automatically on the engagement.)
2. Open `/hangfire` → **Recurring jobs** → `collateral-result-export` → **Trigger now**.
3. Check the output file in `./outbound/` (relative to the API working dir). Every line must be exactly
   202 chars; the trailer count must equal the number of `D` records.
4. Check the ledger: `SELECT * FROM collateral.CollateralResultLogs;`. Trigger again → the same
   appraisal is **not** re-sent. Complete another appraisal (with a host id), trigger again → only the
   new one is emitted.

## Migration

`Modules/Collateral/Collateral/Migrations/*_AddCollateralResultExport.cs` adds:
`CollateralMasters.HostCollateralId`, `CollateralEngagements.{ForcedSaleValue, InternalAppraiserName, LandValue, BuildingValue}`,
`MachineDetails.LifeYear`, and the `CollateralResultLogs` table.
**Apply it yourself** (`dotnet ef database update --context CollateralDbContext …`) — it is not applied automatically here.
