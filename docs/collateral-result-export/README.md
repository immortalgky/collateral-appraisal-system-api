# Outbound "Collateral Result" interface (CAS → AS400)

Ships a fixed-width **231-char** H/D/T file back to the host (AS400) so it can update collateral
prices. Runs on a Hangfire recurring job, **daily at 04:00**, and is driven by a **sent-ledger**
(`collateral.CollateralResultLogs`) so an appraisal that completes *after* a run is picked up by the
next one.

**Ordinary reappraisals only.** This interface is the return leg of COLLATREV: the host sends a
due-list, we review, we report what the review concluded. A brand-new appraisal reaches AS400 through
the loan it belongs to, not through here. The scope is `AppraisalType = 'ReAppraisal'` **and** the
request's Purpose `03`, completed on or after **2026-11-01** (inclusive; `CompletedAt` is local time).
Purpose `09` (block reappraisal) is excluded; it is also stamped `ReAppraisal`, so only the request can
tell it apart. The scope is deliberately **not** a test of who initiated the review. Of 1,606 completed reappraisals on the production-like set only *one* came through the SIBS
channel that COLLATREV itself initiates; the rest are MANUAL, CLS and LOS. Scoping by provenance
would leave a file of one row, and a review is a review whoever started it.

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
| exactly one collateral, not a block project | that id, `Auto Update = N` |
| a block project, units matched | one row per matched unit, each with its own id, `Auto Update = N` |
| a block project, no unit matched | blank id, `Auto Update = N` — even when the walk landed on exactly one collateral, which is used only through a unit match |
| several, not a block project | blank id, `Auto Update = N` |
| nothing | blank id, `Auto Update = N` |

A blank id still identifies the work — the appraisal number is in the record. Guessing which of
several collateral a price belongs to would update the wrong one.

### Auto Update is AS400's own flag, captured at Initiate

`Auto Update = Y` is sent **only** for an appraisal that Initiate raised for a COLLATREV book
(`request.Requests.ReappraisalBookNumber` and `ReappraisalCollateralId` set) **and** whose COLLATREV
row carried `Y` in **position 650** at the moment Initiate ran. Initiate copies the candidate's flag to
`request.Requests.ReappraisalAutoUpdate`; that frozen copy is what the view reads, never the candidate
(which later COLLATREV files keep refreshing). The same request also fixes the collateral id: it
is `ReappraisalCollateralId`, with no chain walk and exactly one row for the appraisal.

Everything else is `N`: a manual request, an Initiate request without a collateral id (it falls back to
the walk), every walk or block result, a request with no flag captured (raised before the column
existed, or from a 649-char file), and a COLLATREV row whose position 650 was blank or not `Y`/`N`.

**Deploy note.** After deploying, ingest a COLLATREV file **before** staff run Initiate. The flag is
only read from files in the 650-char layout; a request raised against a candidate that has no flag yet
freezes `NULL`, and that request goes out `N` for good.

**Deploy note: Initiate drafts raised before this change.** From now on Initiate binds a prior only when it is a
completed, undeleted appraisal. A book whose appraisals are all cancelled or deleted counts as not known to CAS and
takes the legacy path; one with a live appraisal that is not completed (in progress) is skipped. A draft created earlier may point at a prior that fails the submit guard. List
them (read-only) and, after deploy, delete these drafts and run Initiate again. The same SELECT also finds drafts
whose prior was deleted, or stopped being Completed, after Initiate, so run it any time a draft cannot be
submitted, not only after deploy:

```sql
-- Unsubmitted requests Initiate created for a COLLATREV book whose prior would be rejected at submit.
-- "Submitted" = RequestedAt set, or a status other than Draft / New (Request.HasBeenSubmitted).
SELECT r.Id            AS RequestId,
       r.RequestNumber,
       r.ReappraisalBookNumber AS BookNumber,
       CASE WHEN d.PrevAppraisalId IS NULL THEN 'NoPrior'
            WHEN a.Id IS NULL          THEN 'PriorNotFound'
            WHEN a.IsDeleted = 1       THEN 'PriorDeleted'
            WHEN a.Status = 'Cancelled' THEN 'PriorCancelled'
            ELSE                            'PriorNotCompleted' END AS Reason,
       a.Status        AS PriorStatus
FROM request.Requests r
LEFT JOIN request.RequestDetails d ON d.RequestId = r.Id
LEFT JOIN appraisal.Appraisals a   ON a.Id = d.PrevAppraisalId
WHERE r.ReappraisalBookNumber IS NOT NULL
  AND r.IsDeleted = 0
  AND r.RequestedAt IS NULL
  AND r.Status IN ('Draft', 'New')
  AND (   (d.PrevAppraisalId IS NULL AND NULLIF(LTRIM(RTRIM(d.PrevAppraisalNumber)), '') IS NULL)   -- no prior at all
       OR (d.PrevAppraisalId IS NOT NULL
           AND (a.Id IS NULL OR a.IsDeleted = 1 OR a.Status <> 'Completed')))                       -- prior not usable
ORDER BY r.RequestNumber;
```

**Many rows have blank ids, and that is correct.** *Measured before the 2026-11-01 cutoff and before
Initiate requests carried their own collateral id (so it overstates today's blank share):* on the
production-like dataset 1,359 of 1,595 rows went out that way — 85%. It is not a matching failure: for those appraisals the COLLATLINK
feed holds no row at all, at any level of the chain. The dominant cause is that **`PrevAppraisalId`
is NULL on 863 of the 1,606 completed reappraisals (54%)** — with no chain there is nothing to walk,
and the walk recovers the id for only 26 appraisals beyond the 203 whose own number the feed already
carries. That NULL is worth fixing at the source one day; it is not something this view can work
around.

They are sent anyway. That was decided with the bank on 2026-08-31: a result the host never hears
about disappears silently, whereas an `N` row reaches someone who can resolve it. Dropping them was
considered and rejected for that reason — do not "optimise" these rows out of the file.

⚠️ Narrowing the file to reappraisals did **not** improve this ratio — it was 87% before and 85%
after, on 24× fewer rows. The scope change made the file mean the right thing; it did not make the
matching work better.

**An appraisal is sent once.** Any row in `collateral.CollateralResultLogs` for the appraisal means it
is done. A row that went out with a blank id is **not** resent when AS400 later mints an id, and a
block project is not resent when more of its units match later; both are accepted trade-offs of the
once-only rule, so a late match is resolved by the bank by hand.

**Block projects are the exception that resolves.** AS400 mints an id per financed unit and names
that unit twice, in two fields that fail on different rows:

| Key | Read from | Matched against | Needed because |
|---|---|---|---|
| Name token | `CollateralName`, the key inside `CONDO.<key> <deeds>` | `CondoRegistrationNumber`, then `RoomNumber` | the usual condo case |
| Address token | `Address1`, its leading word (`253/36 มบ.มัณฑนา…`) | `HouseNumber` | a **house** in a development has no room number, and its `CollateralName` is a deed number that appears nowhere in the unit table |

The name outranks the address when both find something. The `CONDO` prefix is read leniently —
AS400 writes it as `CONDO.47/18`, `CONDO. 59/38`, `CONDO 159/262` and `CONDO138/133`, and a strict
`CONDO.%` read yields an empty key for every spelling but the first. Same extraction as
`vw_RegulatoryExport`; the two views must not disagree about which unit a collateral is.

The result is one row per financed **collateral**, each with its own price and land area rather than
the development's total repeated. One collateral can name several rooms as a comma list — those are
summed into that collateral's single row, never sent as several rows sharing one id.

A block is decided by the titles' `CollateralType` (32/33, `AppraisalCreationService`), whatever the
purpose, so a manual `03` of a block book still goes through the unit match. A block is sent once, with
the units that matched at that time.

## Sent-ledger, ordering, rejected appraisals

- **Sent-ledger:** `collateral.CollateralResultLogs`, one row per row sent. The export marks the ledger
  after the file is written (at-least-once). Any ledger row for an appraisal excludes it from every
  later run.
- **Order:** `CurrentAppraisalDate` ascending (oldest first), then `AppraisalReportNumber`, then
  `LEN(CollateralId)`, then `CollateralId` — the numeric order of the zero-filled id.
- **Rejected appraisals (`R` rows) are no longer sent.** `AppraisalRejectedConsumer` still spools them
  into `collateral.PendingCollateralResults`, but the export neither reads nor marks that table, so
  `SentAt` stays NULL.

## Where things live

| Piece | Path |
|---|---|
| 231-char writer | `Modules/Integration/Integration/FileInterface/Format/CollateralResult/CollateralResultFileWriter.cs` |
| Export query | `Modules/Integration/Integration/FileInterface/Format/CollateralResult/CollateralResultExportQuery.cs` |
| Row set + id resolution + Auto Update | `Database/Scripts/Views/Collateral/vw_CollateralResultExport.sql` |
| Dry run: the same rows in the same order as the next run, without sending (header date is the preview day; with 0 rows it still returns header + trailer, where the job sends nothing; ignores the config's `IsActive`) | `GET /file-interfaces/admin/collateral-result/preview` |
| Row contract | `Modules/Collateral/Collateral.Contracts/FileInterface/ICollateralResultQuery.cs` |
| Hangfire job | `Modules/Integration/Integration/FileInterface/Jobs/CollateralResult/CollateralResultExportJob.cs` |
| Sent-ledger entity | `Modules/Collateral/Collateral/CollateralMasters/Models/CollateralResultLog.cs` |
| Transport port | `Modules/Integration/Integration.Contracts/FileSink/IOutboundFileSink.cs` |
| Transport impls (Local/Sftp) | `Modules/Integration/Integration/Infrastructure/FileSink/` |
| Recurring registration | `Modules/Integration/Integration/Scheduling/IntegrationRecurringJobs.cs` → job id `collateral-result-export` (daily at 04:00, cron `0 4 * * *`; the schedule is owned by the admin job-schedule UI, the code value only seeds fresh environments) |

Every value in the record is read from the `appraisal` schema by the view; nothing is captured onto
`CollateralEngagement` for this file.

## 231-char Detail layout

Authoritative source: `CollateralResultFileWriter.DetailFields`. Widths sum to exactly 231.

The vendor spec reserves a decimal point in every scale-2 field; we send **implied decimals** instead
(value ×100, no dot), so each `decimal(15,2)` occupies 15 chars rather than 16. That is why positions
here run 10 chars shorter than the spec's own numbering from `Appraisal Value` onward. AS400 confirmed
the implied-decimal layout in the 2026-08 revision, which numbers the fields exactly as below.

| Pos | Field | Width | Source |
|---|---|---|---|
| 1 | Record Type (`D`) | 1 | const |
| 2-20 | Collateral ID (host) | 19 | the Initiate request's `ReappraisalCollateralId`, else `collateral.HostCollateralLinks.HostCollateralId` found by the chain walk above |
| 21-30 | Appraisal Report No | 10 | `appraisal.Appraisals.AppraisalNumber` |
| 31-45 | Appraisal Value | 15 | `appraisal.ValuationAnalyses.AppraisedValue`; a block unit uses its own `ProjectUnitPrices.TotalAppraisalValueRounded` |
| 46-60 | Land Value | 15 | cost approach only: the selected land method's `PricingFinalValues.FinalValueOverride` × the land area of the priced property group (`SelectedPricing` / `SelectedGroupLand` in the view) |
| 61-75 | Building Value | 15 | cost approach only: `PricingFinalValues.BuildingValue` |
| 76-90 | Force Sale Value | 15 | `appraisal.ValuationAnalyses.ForcedSaleValue` |
| 91-98 | Current Appraisal Date | 8 | `appraisal.ValuationAnalyses.ValuationDate` `DDMMYYYY` |
| 99-106 | Next Appraisal Date | 8 | current + 3y |
| 107-110 | Internal Valuer Code | 4 | `auth.AspNetUsers.EmployeeId` of the latest assignment's appraiser — Internal path only, **see below** |
| 111-150 | Internal Valuer Name | 40 | `auth.AspNetUsers` name, falling back to `AppraisalAssignments.InternalAppraiserName` — Internal path only |
| 151-154 | External Valuer Code | 4 | `auth.Companies.HostCompanyCode` — External path only |
| 155-194 | External Valuer Name | 40 | `auth.Companies.Name` — External path only |
| 195-197 | Life Year | 3 | `appraisal.MachineCostItems.LifeSpanYears` (machinery only) |
| 198 | Appraisal Status | 1 | `A` (`R` rows are no longer sent) |
| 199-201 | Building Age (`CCEBIL`) | 3 | oldest building on the appraisal / condo — **see below** |
| 202-208 | Area Utilization (`CCEARE`) | 7 | total building area / condo usable area, sq.m — **see below** |
| 209 | Auto Update | 1 | `Y` only from the flag captured at Initiate (COLLATREV position 650); walk and block rows are `N` |
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
zeros rather than truncated, so one bad collateral cannot abort the nightly run. The age/area rules
are the `PropMix`, `BuildingAgg` and `CondoAgg` CTEs of `vw_CollateralResultExport` (covered by
`CollateralResultScopeTests`); the writer's clamp is the only range guard.

### Internal vs External valuer — one pair per record

An appraisal ran on the External path (an appraisal company produced the book) or the Internal path
(in-house appraiser), never both. Positions 107-150 and 151-194 are therefore **mutually exclusive**:
one pair carries data, the other goes out blank.

The discriminator is `AssigneeCompanyId IS NOT NULL ⇒ External` on the **latest assignment that was not
rejected or cancelled** (the `LatestAssignment` / `Valuer` CTEs of `vw_CollateralResultExport`) — the same
rule the rest of the system uses (see `vw_AppraisalDetail`). The view emits it as an explicit
`IsExternal` flag and `CollateralResultExportQuery.Map` blanks the other pair, rather than inferring the
path from which columns came back populated.

Neither pair can be trusted to be null on its own: on the External path the assignment can still carry
a user id that resolves to an `AspNetUsers` row with an employee id (the bank's follow-up officer, or
the company's own appraiser). Off-system assignments (`EXTO` / `Offline`) count as **External**: the
company did the work even though a bank staffer keyed the book in. An external appraisal whose company
row no longer resolves goes out with all four fields blank — better than a name on the wrong side.
The Offline/EXTO case has no automated test.

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

Rule lives in `CollateralResultExportQuery.ToInternalValuerCode` (unit-tested).
**Asking AS400 to widen this field to 5** would let every appraiser's code go out — recorded as an open
item in `.claude/tasks/as400-host-collateral-link.md`.
Header: `H` + EffectiveDate(`DDMMYYYY`) + filler. Trailer: `T` + 9-char right-aligned detail count + filler.
UTF-8 (no BOM), CRLF line endings. Detail-row grain = one row per pending appraisal (per matched unit
for a block project).

## Configuration

`appsettings*.json`:

```json
"OutboundFileSink": {            // transport — owned by Integration module
  "FileSource": "Local",         // Local | Sftp
  "Local": { "Path": "./outbound" },
  "Sftp": { "Host": "", "Port": 22, "Username": "", "Password": "", "RemoteDirectory": "/outgoing" }
}
```

The file name (prefix, date format, extension) and directory come from the `COLLATERAL_RESULT` row of
`integration.FileInterfaceConfigs`, not from `appsettings`; the default prefix is `COLLATERAL_RESULT_`, giving
`COLLATERAL_RESULT_yyyyMMddHHmmss.txt`.

## Manual testing (dev)

1. Pick (or create) a **Completed** `ReAppraisal` whose request has Purpose `03`, completed on or after
   2026-11-01, with no row in `collateral.CollateralResultLogs`. For an Initiate request the id and
   Auto Update come from the request; for a manual one, an active `collateral.HostCollateralLinks` row
   naming the prior appraisal's number supplies the id.

   **To get an `Auto Update = Y` end to end:** generate a COLLATREV file whose position 650 is `Y`
   with `POST /reappraisal/generate-test-file?count=20&autoUpdate=Y` (permission
   `reappraisal.generate-test-file`; `autoUpdate` is `Y` or `N`, blank when omitted; the file is the
   650-char layout). Let the `reappraisal-as400` job ingest it, then run **Initiate** on one of the
   resulting candidates (the flag is frozen on the request at that moment), submit and complete the
   request, and trigger the export. Only a request raised after the flag was ingested can send `Y`.
   ⚠️ With `autoUpdate=Y` the file's collateral ids are synthetic (60000000 + n), so a run against an
   AS400 that applies `Y` could overwrite a real collateral with a colliding id. Use it only where the
   AS400 side is a test system.
2. Open `/hangfire` → **Recurring jobs** → `collateral-result-export` → **Trigger now**.
3. Check the output file in `./outbound/` (relative to the API working dir):
   ```bash
   awk '{ print length($0) }' outbound/COLLATERAL_RESULT_*.txt | sort -u   # must print 231, once
   cut -c107-150 outbound/COLLATERAL_RESULT_*.txt                          # internal valuer pair
   cut -c151-194 outbound/COLLATERAL_RESULT_*.txt                          # external valuer pair
   cut -c199-208 outbound/COLLATERAL_RESULT_*.txt                          # age + area
   cut -c209-231 outbound/COLLATERAL_RESULT_*.txt                          # auto-update + land area
   ```
   Exactly one valuer pair may be filled per `D` row: an appraisal whose latest assignment has an
   `AssigneeCompanyId` must show blanks at 107-150, one without it must show blanks at 151-194. Cover both by exporting an
   internal and an external appraisal in the same run.
   The trailer count must equal the number of `D` records.
4. Check the ledger: `SELECT * FROM collateral.CollateralResultLogs;`. Trigger again → the same
   appraisal is **not** re-sent. Complete another in-scope appraisal, trigger again → only the new
   one is emitted.
5. To exercise the new fields, cover three shapes: an L&B appraisal with **two or more** buildings
   (area must equal the sum, age the max), a condo, and bare land (both fields all zeros).

## Migration

Schema changes for this interface are EF migrations in the Collateral and Request modules (for example
`AddReappraisalCandidateAutoUpdate` and `AddRequestReappraisalAutoUpdate`), and the view
`collateral.vw_CollateralResultExport` is a DbUp script. The application never applies them itself: run
`dotnet run --project Database/Database.csproj migrate` (in production the DBA runs the generated bundle, see
`deploy/README.md`).
