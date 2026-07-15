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
| Scope | One **Detail** record per active master collateral (`IsDeleted = 0` and `IsMaster = 1`) |
| Encoding | UTF-8 (no BOM), CRLF line endings |
| Record length | **300 characters**, fixed width |
| File name | `REGULATORY_yyyyMMdd.txt` (date = run date) |

**Record layout:** the file has three record types.

| Type | Marker | Content |
|---|---|---|
| Header | `H` | `H` + effective date `ddMMyyyy` (the run date), padded with spaces to 300 |
| Detail | `D` | One per master collateral — the 26 fields below |
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
| 4 | 22–40 | 19 | HOST Collateral ID | decimal(19,0) | `CollateralMaster.HostCollateralId`. Populated by the inbound host-mapping feed; **zeros until that feed provides it**. |
| 5 | 41 | 1 | Collateral Under Construction | string(1) | `Y` / `N` / `L` / blank. **Rule:** not a land / building / land&building type → blank (this includes **Condo**); bare Land or Leasehold land → `L`; building types → `Y` if under construction else `N`. "Under construction" comes from the land detail's *is-under-construction-at-last-appraisal* flag. |
| 6 | 42–46 | 5 | Construction Progress % | decimal(5,2) | Not a land / building / land&building type → `0.00` (this includes **Condo**); bare Land/Leasehold → `100.00`; otherwise the land detail's overall construction-progress percent (bounded 0–100). |
| 7 | 47–61 | 15 | Appraisal Value as Completed | decimal(15,2) | The **latest** engagement's appraisal value. |
| 8 | 62–76 | 15 | Appraisal Value at Origination | decimal(15,2) | If the latest appraisal is a **Progressive** (construction) inspection → the **earliest** engagement's value (the first appraisal already estimated the as-completed value); otherwise the latest value. |
| 9 | 77–79 | 3 | Number of Floors | decimal(3,0) | Building types → the representative building's floor count (bounded 0–999); otherwise `0`. |
| 10 | 80–82 | 3 | Building Age (years) | decimal(3,0) | Building types → representative building's age; Condo → condo detail's building age; otherwise `0` (bounded 0–999). |
| 11 | 83–97 | 15 | Market Selling Price | decimal(15,2) | **Not yet sourced — sent as zeros.** (Pending source confirmation.) |
| 12 | 98–105 | 8 | Valuation Date | YYYYMMDD | The **latest** engagement's appraisal date. |
| 13 | 106–120 | 15 | Valuation Price in Baht | decimal(15,2) | The latest engagement's appraisal value (same figure as field 7). |
| 14 | 121–135 | 15 | Mortgage Value | decimal(15,2) | **Not yet sourced — sent as zeros.** |
| 15 | 136 | 1 | Appraiser Type | string(1) | `1` = external appraisal, `2` = internal. Determined by whether the latest engagement has an external appraisal-company id. |
| 16 | 137 | 1 | Collateral Registration Flag | string(1) | **Not yet sourced — sent blank.** |
| 17 | 138 | 1 | Land Ownership Flag | string(1) | **Not yet sourced — sent blank.** |
| 18 | 139–144 | 6 | DOPA Location | string(6) | 6-digit DOPA sub-district code, matched by sub-district name against the official DOPA sub-district table. Land/building/condo types. |
| 19 | 145–151 | 7 | Land Area (Sq.Wa) | decimal(7,2) | Land types → the land detail's land area (must be ≤ 99,999.99); otherwise zeros. |
| 20 | 152–158 | 7 | Area Utilization (building area) | decimal(7,2) | Building types → representative building's area; Condo → condo detail's usable area (≤ 99,999.99); otherwise zeros. |
| 21 | 159–168 | 10 | Building Type ID | string(10) | Building types → representative building's building-type code; otherwise blank. |
| 22 | 169–268 | 100 | Building Name | string(100) | Building types → the English description of the building-type code (from the BuildingType parameter table); otherwise blank. |
| 23 | 269–276 | 8 | Expected Building Completion Date | YYYYMMDD | **Not yet sourced — sent blank.** |
| 24 | 277–284 | 8 | Construction Review Date | YYYYMMDD | The latest **Progressive** (construction-inspection) engagement date. Blank if the master has none. |
| 25 | 285–292 | 8 | First Valuation Date | YYYYMMDD | The **earliest** engagement's appraisal date. |
| 26 | 293–300 | 8 | Latest Valuation Date | YYYYMMDD | The **latest** engagement's appraisal date. |

**Widths sum to exactly 300.**

---

## How records are selected

- **One record per active master collateral** (`IsDeleted = 0`, `IsMaster = 1`).
- Most value/date fields are driven by the master's **engagements** (its appraisal history):
  - **Earliest** = engagement with the earliest appraisal date → first valuation date + origination value.
  - **Latest** = engagement with the latest appraisal date → completed value, valuation date/price, latest date, and the appraisal number sent in **both** fields 2 and 3.
  - **Latest Progressive** = the latest engagement whose appraisal type is *Progressive* → construction review date.
- **Representative building** (fields 9, 10, 20, 21, 22) = the first (`Sequence = 1`) building recorded on
  the **latest** engagement.

---

## Fields not yet sourced

The following fields are currently sent blank (strings/dates) or zeros (numeric), because the source data
is not yet captured in the system. Each needs a source decision before it can be populated:

| # | Field | Status |
|---|---|---|
| 4 | HOST Collateral ID | Column exists; awaits the inbound host-mapping feed to populate it. |
| 11 | Market Selling Price | No source column yet — needs a source decision. |
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
