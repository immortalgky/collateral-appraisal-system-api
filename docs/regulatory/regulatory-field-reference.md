# CAS → AS400 Regulatory Interface — Field Reference

This is the field-by-field reference for the monthly **CAS → AS400 Regulatory** (Basel/RDT) interface
file. It describes every field of the record: its position, length, type, **where the value comes
from**, and the **condition** that decides the value. It is written to be readable by non-IT users who
need to check the data that was sent.

> A human-readable **Excel companion** (`REGULATORY_yyyyMMdd.xlsx`) is produced alongside the interface
> file each month with these same fields under friendly column headers — open that to inspect the data.

---

## File at a glance

| Property | Value |
|---|---|
| Direction | Outbound — CAS → AS400 |
| Frequency | Monthly (1st of the month, 02:00), full snapshot — **not** incremental |
| Scope | One **Detail** record per **appraisal application** — represented by the **latest appraisal of its chain** — for collateral the bank **currently holds**. See "Record scope" below. |
| Encoding | UTF-8 (no BOM), CRLF line endings |
| Record length | **300 characters**, fixed width |
| File name | `REGULATORY_yyyyMMdd.txt` (date = run date) |

### Record scope — what counts as one record

One Detail record = **one appraisal application**, represented by the **latest appraisal in its
chain**, and only for collateral the bank **currently holds**.

- **Chain.** Successive appraisals of the same facility are linked by
  `appraisal.Appraisals.PrevAppraisalId` — a reappraisal, a construction-inspection round, or an
  appeal points back at the appraisal it followed. A chain therefore belongs to **one customer's
  facility**. Everything in a chain collapses into **one** record: the *latest* appraisal supplies
  the current values, the *first* appraisal in the chain supplies the origination values
  (fields 7–8).
- **Not redeemed.** A record is excluded only once the nightly `HOST_COLLATERAL_LINK` feed has
  explicitly reported the collateral as `'R'` (redeemed) — `CollateralMaster.IsRedeemed = 1`.
  Collateral the feed has said nothing about yet stays in the file: "no news" is not the same as
  "released", and excluding it would hide collateral the bank holds. Field 4 is then blank.
- **Real estate only — an allow-list.** `CollateralType IN ('L','LB','U','LSL','LSB','LS','LSU','UNK')`.
  Land, land-and-building and condo units; leasehold over land or a condo counts as real estate
  (product owner, 16 Aug 2026). Machinery (`MAC`) and block projects (`PRJ`) are out. It is written
  as an allow-list on purpose: the previous `<> 'PRJ'` looked correct only because no machinery
  master happened to exist, and the first one created would have gone to the regulator unannounced.
- **AS400 legacy collateral is included.** Collateral held since before this system existed was
  valued inside AS400 and never appraised in CAS; it carries `CollateralType = 'UNK'` and an
  appraisal number in the `99A…` series. Its engagement has no row in `appraisal.Appraisals` — by
  design — so the chain walk uses a LEFT JOIN and treats it as the root of its own chain.
  **Fields 18 (DOPA sub-district), land area, building age and usable area go out blank**: the AS400
  legacy listing carries no title number and no location, and there is no other source. Accepted by
  the business.
- **Known duplication.** A collateral that has both an AS400 legacy valuation and a newer CAS
  appraisal produces **two** records, because nothing links the two chains. 242 such collateral on
  the production-like set. The business accepted this to get the legacy portfolio reported at all;
  collapsing them would mean rewriting the CAS chain root's `PrevAppraisalId` to point at the legacy
  appraisal.
- **Block projects are not sent yet.** A block-project appraisal covers a whole development, and the
  bank does not hold the development — it holds the individual units it financed. AS400 issues one
  collateral id per financed unit, so the intended record is **one per unit the bank holds**, valued
  at that unit's own appraised value. That view does not exist yet, and until it does no id is
  recorded against a project appraisal, so block projects produce no records at all. Sending one line
  per project would report the whole development's selling price against a single unit's collateral
  id, and a single unit's redemption would remove the entire development from the file.
  **This is an under-report, and the bank needs to accept it while it stands.** Building the per-unit
  view is paused pending confirmation from the bank — see `.claude/tasks/as400-host-collateral-link.md`.

> **Changed from the previous version.** This file used to send one record per *collateral master*
> (one physical property), aggregating every appraisal ever made against it. Because a property can
> change hands, that mixed several customers' histories into one record — the origination value could
> come from a previous owner's loan while the current value came from the present owner's. Grouping
> by chain removes that.

**Record layout:** the file has three record types.

| Type | Marker | Content |
|---|---|---|
| Header | `H` | `H` + effective date `ddMMyyyy` (the run date), padded with spaces to 300 |
| Detail | `D` | One per appraisal chain (see "Record scope") — the 26 fields below |
| Trailer | `T` | `T` + total detail count (9 digits, zero-padded), padded with spaces to 300 |

**Number formatting (important):** money/decimal fields are written as **implied-decimal, no decimal
point** — the value is multiplied by 100 and left-padded with zeros. Example: `5,000,000.50` is written
as `500000050`. A blank/absent numeric field is written as all zeros. Detail date fields are `YYYYMMDD`
(e.g. `20250121`); a blank date is spaces. (The Excel companion, by contrast, shows real decimals and
`dd/MM/yyyy` dates for readability.)

---

## Detail record — all 26 fields

Positions are 1-based. "Type" is the logical type; on the wire, `decimal(x,2)` fields drop the decimal
point (×100, zero-filled). "Building types" = Land&Building (LB), Leasehold Building (LSB), Leasehold
w/ Building (LS). "Land types" = Land (L), LB, Leasehold Land (LSL), LSB, LS.

| # | Pos | Len | Field | Type | Where the value comes from / condition |
|---|-----|-----|-------|------|----------------------------------------|
| 1 | 1 | 1 | Record Type | string(1) | Constant `D` (Header `H`, Trailer `T`). |
| 2 | 2–11 | 10 | Application Id (appraisal no.) | string(10) | The **latest** engagement's appraisal number — same value as field 3. The bank always sends the latest appraisal report number in this field. |
| 3 | 12–21 | 10 | Newest Application Id (latest appraisal no.) | string(10) | The **latest** engagement's appraisal number. |
| 4 | 22–40 | 19 | HOST Collateral ID | decimal(19,0) | `CollateralMaster.HostCollateralId` — the current AS400 id of the collateral itself, populated by the nightly `HOST_COLLATERAL_LINK` feed. Zeros when the feed has not supplied one yet, which is not a reason to withhold the record (see "Record scope"). |
| 5 | 41 | 1 | Collateral Under Construction | string(1) | `Y` / `N` / `L` / blank. **Rule:** not real estate (Machinery, PRJ) → blank; bare Land or Leasehold land → `L`; **every other real-estate type — LB/LSB/LS, Condo (U/LSU) and legacy UNK** — → `Y` if under construction else `N`. "Under construction" is the chain tip engagement's `IsUnderConstruction`. |
| 6 | 42–46 | 5 | Construction Progress % | decimal(5,2) | Not real estate → `0.00`; bare Land/Leasehold land → `0.00`; **every other real-estate type incl. Condo and legacy UNK** → `100.00` if **completed** (not under construction), else the overall construction-progress percent (bounded 0–100). |
| 7 | 47–61 | 15 | Appraisal Value as Completed | decimal(15,2) | The value **as it stands today**. When nothing is under construction this is simply the latest engagement's appraisal value. When buildings are part-built it is the progress-adjusted figure: **land + buildings with no inspection + inspected buildings at their current progress**. See "Current value" below. |
| 8 | 62–76 | 15 | Appraisal Value at Origination | decimal(15,2) | The **latest** engagement's appraisal value — always, with no condition on appraisal type. (The previous rule of substituting the earliest value for a Progressive inspection was removed at the bank's request.) This therefore carries the same figure as field 13. |
| 9 | 77–79 | 3 | Number of Floors | decimal(3,0) | Building types → the representative building's floor count (bounded 0–999); otherwise `0`. |
| 10 | 80–82 | 3 | Building Age (years) | decimal(3,0) | Building types → the age of the **oldest** building on the engagement (`MAX`); Condo → condo detail's building age; otherwise `0` (bounded 0–999). |
| 11 | 83–97 | 15 | Market Selling Price | decimal(15,2) | `request.RequestDetails.TotalSellingPrice` of the chain tip's originating request (joined via `CollateralEngagement.RequestId`). Request-level — one value per request. Blank/zeros if none recorded. |
| 12 | 98–105 | 8 | Valuation Date | YYYYMMDD | The **latest** engagement's appraisal date. |
| 13 | 106–120 | 15 | Valuation Price in Baht | decimal(15,2) | The latest engagement's appraisal value (same figure as field 7). |
| 14 | 121–135 | 15 | Mortgage Value | decimal(15,2) | **Not yet sourced — sent as zeros.** |
| 15 | 136 | 1 | Appraiser Type | string(1) | `1` = external appraisal, `2` = internal. Determined by whether the latest engagement has an external appraisal-company id. |
| 16 | 137 | 1 | Collateral Registration Flag | string(1) | **Not yet sourced — sent blank.** |
| 17 | 138 | 1 | Land Ownership Flag | string(1) | **Not yet sourced — sent blank.** |
| 18 | 139–144 | 6 | DOPA Location | string(6) | 6-digit DOPA sub-district **geocode** from `request.RequestDetails.SubDistrict` — the request's "Location" section, which is the administrative (DOPA) address. Validated against `parameter.DopaSubDistricts.Code`; an unknown value → blank. Land/building/condo types. **See the note below.** |
| 19 | 145–151 | 7 | Land Area (Sq.Wa) | decimal(7,2) | Land types → the land detail's land area (must be ≤ 99,999.99); otherwise zeros. |
| 20 | 152–158 | 7 | Area Utilization (building area) | decimal(7,2) | Building types → the **total** area of every building on the engagement (`SUM`); Condo → condo detail's usable area. Zeros when the figure exceeds 99,999.99 — the guard applies to the total, since two buildings that each fit can overflow together. Otherwise zeros. |
| 21 | 159–168 | 10 | Building Type ID | string(10) | Building types → representative building's building-type code; otherwise blank. |
| 22 | 169–268 | 100 | Building Name | string(100) | Building types → the English description of the building-type code (from the BuildingType parameter table); otherwise blank. |
| 23 | 269–276 | 8 | Expected Building Completion Date | YYYYMMDD | **Not yet sourced — sent blank.** |
| 24 | 277–284 | 8 | Construction Review Date | YYYYMMDD | The **latest appraisal's date** when the collateral is under construction — any appraisal that reviewed the construction counts, not only a Progressive-type one. **Blank when it is not under construction**, since there is no construction left to review. |
| 25 | 285–292 | 8 | First Valuation Date | YYYYMMDD | The **earliest** engagement's appraisal date within this (chain, master). |
| 26 | 293–300 | 8 | Latest Valuation Date | YYYYMMDD | The **latest** engagement's appraisal date. |

**Widths sum to exactly 300.**

### Note on field 7 — the "current value" while a building is under construction

Field 7 answers *"what is this collateral worth today?"* — which differs from the appraised value only
while a building is still going up.

| Situation | Field 7 |
|---|---|
| Nothing under construction | The appraised value (same as fields 8 and 13) |
| Buildings part-built | land **+** buildings that were already finished **+** part-built buildings at their construction progress |

Worked example — land 6,000,000 and one building appraised at 4,000,000, construction 50% complete:

```
6,000,000 + (4,000,000 × 50%) = 8,000,000
```

Field 7 reports **8,000,000**, while fields 8 and 13 report the full **10,000,000**. The two figures are
*meant* to differ here: field 7 is today's value, fields 8 and 13 are the value once finished.

Three things worth knowing:

- **Progress comes from the construction inspection's recorded percentage**, not from the value the
  inspector typed. Those two are entered independently and nothing forces them to agree; the percentage
  is the one the system reliably saves.
- **A building with no inspection counts at full value** — no inspection means it was already complete.
- **When construction reaches 100%** the collateral is no longer flagged as under construction, so
  field 7 goes back to being the plain appraised value.

The same calculation drives the construction card on the **Decision Summary** screen, so a figure
checked on screen and the figure in this file are produced by one piece of code and cannot disagree.

### Note on field 18 — why the DOPA code does not come from the collateral's own address

There are **two different addresses** for a property and they are mastered separately:

| Address | Stored on | Master |
|---|---|---|
| **Deed** (ที่อยู่ตามโฉนด) | `collateral.LandDetails` / `CondoDetails` `.SubDistrict` | `parameter.TitleSubDistricts` |
| **Administrative / DOPA** | `request.RequestDetails.SubDistrict` | `parameter.DopaSubDistricts` |

The two masters **diverged on 2026-07-29** when the Title tables were re-seeded from the Land
Department list: 11,144 Title sub-districts against 7,436 DOPA ones, and **3,715 Title codes exist in
no DOPA table**. Until 2026-08-09 this field read the *deed* code and validated it against the *DOPA*
master, which meant two failures at once — codes that exist only in the Title master came out blank,
and codes present in both came out as whatever the deed said rather than the administrative address.
On dev data 5 of 11 exported rows changed value when this was corrected.

**Known limitation:** `RequestDetails` is request-level, so every collateral on one request reports the
same sub-district. The per-property source is
`appraisal.{Land,Condo}AppraisalDetails.DopaSubDistrict` (added by `7f366e4b`), which is already carried
on `AppraisalForCollateralResult` but is populated on 1 of 105,469 rows today versus 99.99% coverage
from `RequestDetails`. Moving to it needs that column backfilled **and** carried onto
`collateral.LandDetails` / `CondoDetails`, which do not have it yet.

---

## How records are selected

- **One record per active master collateral** (`IsDeleted = 0`, `IsMaster = 1`).
- Most value/date fields are driven by the **engagements in the chain** (its appraisal history):
  - **Earliest** = engagement with the earliest appraisal date → first valuation date + origination value.
  - **Latest** = engagement with the latest appraisal date → completed value, valuation date/price, latest date, and the appraisal number sent in **both** fields 2 and 3.
  - **Latest Progressive** = the latest engagement whose appraisal type is *Progressive* → construction review date.
- **Representative building** (fields 9, 10, 20, 21, 22) = the first (`Sequence = 1`) building recorded on
  the **latest** engagement.
- **Selling price** (field 11) = the latest engagement's originating request, via `CollateralEngagement.RequestId`
  → `request.RequestDetails.TotalSellingPrice` (request-level, one value per request).

---

## Fields not yet sourced

The following fields are currently sent blank (strings/dates) or zeros (numeric), because the source data
is not yet captured in the system. Each needs a source decision before it can be populated:

| # | Field | Status |
|---|---|---|
| 14 | Mortgage Value | No source column yet — needs a source decision. |
| 16 | Collateral Registration Flag | No source column yet — needs a source decision. |
| 17 | Land Ownership Flag | No source column yet — needs a source decision. |
| 23 | Expected Building Completion Date | No source column yet — needs a source decision. |

---

## Note on the older spec documents

The original spec (`.claude/docs/CAS-AS400-Regulatory.xlsx`) and the flow diagram
(`docs/regulatory/cas-as400-flow.html`) describe a **308-character** record with **inclusive decimal
symbols** (decimal points in numbers). The **live interface is 300 characters with implied-decimal
numbers (no decimal point)** — the shorter, no-point format is authoritative. Treat this document as the
current source of truth for the field layout.
