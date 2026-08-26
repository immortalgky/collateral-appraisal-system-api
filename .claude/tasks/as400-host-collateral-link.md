# AS400 Host Collateral Link — collateral id exchange and the Regulatory report

This document describes the state **after** the work landed: both directions of the AS400 exchange
and the reasoning behind the design.

---

## The principle behind every decision here

> **The appraisal number is the only key the two systems share.**
> Collateral identity is not shared and never will be, because AS400 and CAS decompose collateral
> differently.

Every attempt to join on collateral identity (storing the id on the master, matching counts, aligning
each side's "IsMaster" election) produced a defect. Every time the join moved to the appraisal number,
the defect disappeared.

**AS400's collateral id is an opaque token that we echo back.** We never interpret it and never match
it to a row of ours. AS400 elects its master row by its rule and we elect ours by ours; the two need
not denote the same thing.

---

## Inbound (AS400 → CAS)

### `HOST_COLLATERAL_LINK` — collateral id and pledge state

| | |
|---|---|
| File | `AS400_COLLATLINK_YYYYMMDD.txt` |
| Frequency | **Monthly file, full replace.** The job runs daily (05:00) because delivery time is not guaranteed and a monthly schedule that fired early would skip the file for a whole month. `collateral-result-export` no longer waits for it. |
| Length | 39 chars (H / D / T) |
| Spec | `.claude/docs/AS400-COLLAT.xlsx`, sheet "Interface File" |
| Parser | `HostCollateralLinkFileParser` |
| Job | `As400HostLinkJob` |
| Lands in | `collateral.CollateralEngagements` — three columns |

**Detail record**

| Pos | Field | Type |
|---|---|---|
| 1 | Record Type | `'D'` |
| 2–11 | Appraisal Report Number | string(10) — our appraisal number (AS400 calls it CCSURV) |
| 12–30 | Collateral ID | decimal(19,0) — CCDCID, **the master collateral only** |
| 31–38 | Date | DDMMYYYY — **the date the file was transmitted, not the event date.** Every row in a file carries the same value (verified across all 32,662 rows of 2026-08-04), so it cannot order events and must never be used to. Ordering across files uses the date in the file name, held on `HostCollateralLinks.LastSeenFileDate`. |
| 39 | Record Indicator | `'D'` pledged / `'R'` redeemed (the spec marks this Use = Basel) |

**AS400 sends only the master collateral's id**, and resends when its master changes — the `RowHash`
differs, so the ingestor overwrites automatically.

**The file replaces the whole set.** Rows it stops listing are collateral the bank no longer holds.
They keep their previous `LastSeenFileDate`, which puts them outside the active set without deleting
anything, and every reader filters on that column. A file older than the one already applied is
refused outright rather than rolling the table back.

**Except for block projects, where the grain is the unit** — see "Block projects" below.

**Partial pledge and partial redemption are not supported.** The file carries no per-title identifier,
so a redeemed id cannot be resolved to one of our titles, and our valuations are per group rather than
per title. Confirmed out of scope with the business owner.

### `REAPPRAISAL` (COLLATREV) — reappraisal due list

Monthly, 649 chars, lands in `collateral.ReappraisalCandidates`.
**This is a due-list only. Never use its `CollateralId` to update host collateral ids.**

---

## Outbound (CAS → AS400)

### `COLLATERAL_RESULT` — appraisal results

Daily at 00:00, 208 chars, **one row per collateral master**, carrying that master's *latest*
engagement figures. The collateral id is read from `CollateralMaster.HostCollateralId` and written at
positions 2–20. `CollateralResultLogs` (unique on `AppraisalId`) still prevents duplicate sends — the
key is the latest engagement's appraisal, so a new appraisal sends once and a re-run sends nothing.

The 2026-08 spec revision appended `CCEBIL` Building Age (199–201) and `CCEARE` Area Utilization
(202–208), taking the record from 198 to 208. Positions 1–198 are untouched, but the length change is
still breaking for the host's reader — **coordinate the cut-over date before deploying**.

Rejected appraisals go out as `'R'` rows with a blank CCDCID, which is correct: AS400 mints ids at
drawdown, which a rejected appraisal never reaches.

### `REGULATORY` — collateral portfolio (Basel/RDT)

Monthly, 300 chars, from `collateral.vw_RegulatoryExport`.
Field reference: `docs/regulatory/regulatory-field-reference.md`.

**One record per appraisal application**, represented by the **latest appraisal of its chain**.
Everything is reported except block projects and collateral AS400 has explicitly released
(`CollateralMaster.IsRedeemed = 1`) — silence from the feed is not "released".

---

## Why the collateral id lives on `CollateralMaster`, not `CollateralEngagement`

| | `CollateralMaster` | `CollateralEngagement` | AS400 |
|---|---|---|---|
| Grain | **one physical collateral** | 1:1 with an appraisal | **one physical collateral** |
| Mutable | yes — current state | no — frozen at completion | yes — current state |

The file *addresses* rows by appraisal number, and that is what misled the first design: an
engagement is UNIQUE on `AppraisalId`, so it looked like the matching grain. But addressing is not
meaning. What the message *describes* is the collateral — AS400 mints one id per collateral at
drawdown and reports redemption against that same id, with no notion of which appraisal is involved.

Holding it per appraisal made every reader re-derive "which appraisal speaks for this collateral
right now", and each did it differently: the outbound file took the appraisal's own row, the
regulatory view needed a dedicated CTE to find the latest appraisal *carrying an id* (inspections and
revaluations involve no drawdown, so the newest appraisal often has none), and the master view
derived a third answer. One mutable column on the master replaces all three.

The engagement remains how an incoming row is *resolved* — appraisal number → engagement → master —
but nothing is written to it.

**No separate ledger table** is needed: the master row is the ledger, and it already exists by the
time an id arrives, because AS400 mints it at drawdown — after the appraisal completes.

**Redemption reaches the alias rows too.** A group's other titles are separate `CollateralMasters`
rows (`IsMaster = 0`) holding no engagements, so nothing in the ingest loop reaches them; left
unflagged they would keep being reported to the regulator as still held. Only the flags propagate —
AS400 issued one id for the whole group.

---

## Block projects: AS400 issues one id per financed unit

A block project is **one** appraisal, **one** PRJ `CollateralMaster` and **one** `CollateralEngagement`,
with the units as child rows in `collateral.ProjectUnits`. AS400 decomposes it differently: it mints a
collateral id for **each unit that has been sold and financed by the bank** — unsold units and units
financed elsewhere never get one — and stamps the **project's** appraisal number in CCSURV on all of
them.

So one project appraisal arrives as N rows sharing an appraisal number and carrying different ids,
against a single master with a single id slot. The grain is off by one level again: **a master is one
physical collateral, but for a block the physical collateral AS400 finances is the unit.**

**Where a unit's id lives:** `collateral.ProjectUnits.HostCollateralId`.

**Written by** `HostCollateralIdBackfillJob` (Part 2), from `appraisal.ProjectUnits.HostCollateralId`,
which the legacy-system migration populated. Source rows come from the appraisal that last upserted the
master (`ProjectDetails.LastAppraisalId`), matched on sequence number plus room/plot; anything that does
not match is counted and logged rather than attached to a neighbouring unit.

**Preserved by** `CollateralMasterUpsertService.CarryHostCollateralIds`. `ProjectDetail.ReplaceUnits`
rebuilds the entire unit set on every appraisal of the project and the appraisal snapshot carries no
host id, so without the carry-over every block reappraisal would erase the ids.

**Not written by the nightly feed.** `HostCollateralLinkIngestor` skips any row whose engagement's
`AppraisedCollateralType` is `PRJ`, logs every id it received for it, and reports the count as
`ProjectSkipped`. This enforces one rule:

> **One redeemed unit must never remove the whole project from the regulatory report.**

Under the collapse rule (`PickWinningRecord`: newest date wins, `'R'` breaks ties) a single redemption
had the newest `RecordDate`, so it won and stamped the project redeemed — and
`vw_RegulatoryExport`'s redemption filter then dropped every unit of that project. Skipping leaves the
PRJ master's id NULL; block projects are in any case excluded from the export by type. Absent is
wrong, but one arbitrary unit's id carrying the whole project's value is worse, and silently losing a
whole project's exposure is worse still.

**The file does carry a unit key — packed into CCSURV.** Reported by the business owner on
2026-08-09: for a block project AS400 writes the project's 8-digit appraisal number followed by a
2-digit unit sequence, filling the 10-character field exactly. An ordinary appraisal leaves the last
two characters blank.

```
69003747 = the project's appraisal number

D6900374701  25909  25012025D   <- unit sequence 1
D6900374702  25910  25012025D   <- unit sequence 2
D6900374712  25911  25012025R   <- unit sequence 12
D69003748    25912  25012025D   <- ordinary appraisal: 8 digits + 2 spaces
```

The sequence is **our** `ProjectUnit.SequenceNumber`.

**Not yet implemented, and not yet confirmed in writing.** Two things must be settled with the bank
before building on this (see Open items):

- **`.claude/docs/AS400-COLLAT.xlsx` does not document the packing** — it is currently hearsay.
- **Two digits cap the scheme at 99 units, and condos run to hundreds.** What AS400 sends for unit 100
  and beyond is unknown; if it wraps, a wrapped sequence would attach one unit's id to another.

Until then a PRJ row cannot be attributed. Note the current failure mode: a packed number matches no
engagement (the lookup is exact-match on `AppraisalNumber`), so those rows land in `notFound`, whose
warning blames `AppraisalCompletedConsumer` dead-lettering — **a misleading diagnosis for this case.**
`ProjectSkipped` stays 0 because its guard runs only after an engagement is found.

## Chain-based grouping

A chain is the set of appraisals linked by **`appraisal.Appraisals.PrevAppraisalId`** (reappraisal
`03`, block project `09`, construction inspection `06`/`11`, appeal `12`). **A chain belongs to a
single customer by construction**, which makes it the correct unit for the regulatory report.

Grouping by `CollateralMasterId` previously mixed several owners' histories into one row — an earlier
owner's origination value sitting beside the current owner's latest value.

This also removes the need to fork masters per owner, which was considered and rejected: it would have
required re-keying four unique indexes, a data migration, and reworking the PDPA redaction script that
overwrites `OwnerName`, all without any CIF to key on.

> `appraisal.Appraisals.PrevAppraisalId` is written **once** at appraisal creation
> (`Appraisal.cs:117`) and is not re-synced when the request is edited later. The team's decision is to
> **block editing `PrevAppraisalId` once an appraisal exists** rather than sync it. If editing is ever
> re-enabled, `vw_RegulatoryExport` and `GetPreviousAppraisalChainQueryHandler` must both be revisited.

### Never put a depth cap on the recursive CTE

The old `GetPreviousAppraisalChain` used `WHERE c.Depth < 20` together with
`OPTION (MAXRECURSION 20)`. The first predicate stops the recursion before `MAXRECURSION` can fire, so
**there is no error — a chain longer than 20 is silently truncated and the 20th ancestor is returned
as the root.**

Chains beyond 20 are reachable in practice through **construction inspections**, which can run to
dozens per project.

Both `vw_RegulatoryExport` and `GetPreviousAppraisalChainQueryHandler` therefore use a **Path-based
cycle guard** (`CHARINDEX` over the visited path) with `MAXRECURSION 0` — terminating only on a real
cycle. Verified against a 34-level chain. Note `OPTION (MAXRECURSION 0)` cannot live inside a view, so
`RegulatoryExportQuery` supplies it on the outer query.

Both must use the **same column and the same guard**, or the history screen and the report would
disagree about what the chain is.

---

## Master resolution: dedup key first, chain as fallback

```
1. find the master by the collateral's dedup key (physical attributes)   ← primary
2. miss + PrevAppraisalId set → use the previous appraisal's master      ← fallback
3. both miss → create a new master
```

`FindMasterViaPreviousAppraisalAsync` in `CollateralMasterUpsertService` covers land, condo, machinery
and leasehold. It is deliberately narrow:

- only for `ReAppraisal` / `Progressive` appraisals, which are the same property by definition
- the land path additionally requires province / district / sub-district to match
- it refuses a master already claimed by another property group in the same appraisal
- **every use logs a warning**, because it means two appraisals' identifying data disagree

This recovers small title-number drift, which previously created a spurious new master or raised the
cross-group `ConflictException` that dead-letters without retry and has no merge tool.

**The chain is not used as the primary key** because a first appraisal has no chain, and doing so would
make "has this land been appraised before?" unanswerable — see `PRJ`, which uses lineage alone and
creates a fresh master whenever lineage breaks.

---

## Open items

- **Per-unit pipeline for block projects** — paused 2026-08-09 pending the bank's answers. Full plan
  with field-by-field mapping: `~/.claude/plans/dapper-gliding-lake.md`. In dependency order:
  1. **Confirm with the bank, before any code:**
     (0) does the regulatory file need block projects at all — a "no" closes this entire item;
     (a) what AS400 sends for unit 100+, since two digits stop at 99 — the answer should be to widen
     the 39-character record so the sequence gets 3–4 digits;
     (b) whether outbound files should echo the packed 10-digit number or keep the plain 8-digit one;
     (c) get the 8+2 packing written into `.claude/docs/AS400-COLLAT.xlsx`. Note our appraisal number
     has no width guard (`AppraisalUnitOfWork.cs` formats the running number with `D6`), so a year that
     exceeds 999,999 appraisals would make it 9 digits and break any fixed 8+2 split — match the full
     number first, then fall back to the split.
  2. Parse CCSURV as 8+2 and write to the unit, matching on `HostCollateralId` first and
     `SequenceNumber` only as a fallback — sequence numbers are re-derived positionally by
     `ReplaceUnits` and shift whenever the unit set changes.
  3. `vw_RegulatoryExport` emits **one record per financed unit** for PRJ masters: unnest
     `collateral.ProjectUnits`, use each unit's `LastAppraisedValue` instead of the project's
     `ProjectSellingPrice`, and move the redemption filter down to the unit so a redemption removes
     only that unit's line.
  4. `COLLATERAL_RESULT` per unit requires re-keying `UX_CollateralResultLogs_Appraisal` from
     `AppraisalId` to `(AppraisalId, HostCollateralId)`; today it structurally forbids more than one
     outbound row per appraisal.
  5. `ProjectUnit` gains `IsRedeemed` / `RedeemedDate` once the feed can write per unit, mirroring
     what `CollateralMaster` now carries. They are
     deliberately absent now: the backfill's only source is legacy drawdown data with no date.
     Whatever adds them must also extend `CarryHostCollateralIds` to carry all three fields — carrying
     the id alone would leave every unit looking unpledged after a block reappraisal, dropping the
     whole project from the report again.
  - Field 18 (DOPA sub-district) has **no source for units**: `collateral.ProjectDetails` stores only a
    free-text `Address` and `Province`, while `appraisal.Projects.Address` holds the sub-district
    geocode. Step 3 needs `SubDistrict` added to `ProjectDetail` and populated in `UpsertProjectAsync`.
- **`InternalValuerCode` is one character too narrow** — the `COLLATERAL_RESULT` field (positions
  107-110) is 4 characters, but `auth.AspNetUsers.EmployeeId` is 5. Leading zeros are stripped, which
  covers the `0NNNN` shape (554 of 576 staff on the current data), but **21 staff have five significant
  digits** (`81018`, `90378`, …) and their rows go out with a blank code — deliberately, since the
  writer would otherwise truncate `81018` to `8101` and name a different employee. **Ask AS400 to widen
  the field to 5 (record 208 → 209)**; every code would then fit. See
  `CollateralResultQuery.ToInternalValuerCode` and `docs/collateral-result-export/README.md`.
- **Orphaned columns** — `HostCollateralId` on the `appraisal` tables is a legacy-migration input read
  only by `HostCollateralIdBackfillJob`. Dropping it should be decided separately, and only after the
  backfill has run in production.
- **Check on production** — the real maximum chain depth, and how many completed appraisals lack an
  engagement (on dev these are all pre-3 May 2026, i.e. before the Collateral module existed).
- **`Tests/Integration`** — was blocked by the `MarketComparableTemplateFactors` seed failing on a
  fresh database; the seed now skips unknown factor codes instead of aborting. Factor codes `73` and
  `74` are still referenced but undefined, and their real definitions must come from the LHB Parameter
  Listing spreadsheet.
