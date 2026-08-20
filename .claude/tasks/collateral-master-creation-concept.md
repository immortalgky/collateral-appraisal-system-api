# CollateralMaster — how one appraisal becomes collateral rows

This document describes the state **after** the leasehold work landed (2026-08-15): what
`CollateralMasterUpsertService` produces for a completed appraisal, which row is the principal one,
and what happens when the data is incomplete.

Companion documents:
- `.claude/tasks/as400-host-collateral-link.md` — the AS400 exchange (inbound ids, outbound files)
- `docs/regulatory/regulatory-field-reference.md` — the 300-char Regulatory layout, field by field

Entry point: `AppraisalCompletedConsumer` → `CollateralMasterUpsertService.ProcessAppraisalAsync`
(`Modules/Collateral/Collateral/CollateralMasters/Services/CollateralMasterUpsertService.cs:46`).

---

## 1. What a CollateralMaster is

**One row per physical collateral, forever.** It is owner-agnostic and it is *not* per appraisal: the
same parcel resold twice and reappraised five times is still one row. `CollateralType` discriminates
(`L`, `LB`, `U`, `LSL`, `LSB`, `LS`, `LSU`, `MAC`, `PRJ` — `Collateral.Contracts/CollateralTypes.cs`)
and one 1:1 detail table hangs off it per family: `LandDetails`, `CondoDetails`, `LeaseholdDetails`,
`MachineDetails`, `ProjectDetail`.

What changes per appraisal lives on `CollateralEngagement` instead — see §6.

## 2. IsMaster and aliases

`IsMaster = true` is the group's canonical row. `IsMaster = false` + `ParentMasterId` is an **alias**
(`CollateralMaster.cs:125,130`).

- **Land with several title deeds** → one IsMaster plus one alias per remaining title, because the
  land dedup key is per title. Heavy data lives on the IsMaster; aliases carry key columns only.
- **One collateral per appraisal**: every group that is not the primary becomes a *typed* alias of
  the primary — it keeps its own CondoDetail / MachineDetail / LeaseholdDetail, only
  `IsMaster`/`ParentMasterId` mark it as subordinate (`CreateCondoAlias`, `CreateMachineAlias`,
  `CreateLeaseholdAlias`, `DemoteToAlias`).
- **An alias can never own an engagement.** `DemoteToAlias` throws when the row already has one
  (`CollateralMaster.cs:262`): a row that was appraised standalone is a collateral in its own right
  and must keep its identity for cross-appraisal reuse. The upsert service checks
  `Engagements.Count == 0` before demoting anything and logs a warning when it declines.

## 3. Electing the primary — who gets the engagement

Decided at `CollateralMasterUpsertService.cs:144-149`, from property data alone (no DB access), so it
is stable for the whole method:

1. **Any `L`/`LB` property present → the collapsed land master is the primary, unconditionally.**
   Confirmed product rule; it holds even when the appraisal also contains a leasehold.
2. Otherwise → the group with the lowest `GroupNumber` (ties keep original property order).
3. **Fallback** (`:276`) — if the elected group ends up producing no master at all, promote the first
   master that did resolve, walking groups in the same order. Added because the leasehold path now
   warns-and-skips instead of throwing: without it, one half-filled lease contract in the elected
   group cost the *whole appraisal* its engagement — and with it the HostCollateralId and every
   outbound interface — while other groups had resolved perfectly well.

## 4. Dedup keys, and the chain fallback

| Type | Key |
|---|---|
| Land | Province + District + SubDistrict + TitleNumber (four columns since 2026-08-09) |
| Condo | CondoRegistrationNumber + Building + Floor + Room + Province + District + SubDistrict |
| Leasehold | LeaseRegistrationNo (ContractNo) + **UnderlyingMasterId** + Lessor + Lessee + LeaseTermStart |
| Machine | tier 1: RegistrationNumber · tier 2 (when absent): SerialNo + Brand + Model + Manufacturer |

Every key column is NOT NULL on its detail table, so an incomplete key can never mint a master — the
resolver has to fall through to another source rather than fail inside `SaveChangesAsync`.

**Chain fallback** (`FindMasterViaPreviousAppraisalAsync`, `:700`). The dedup key must match
character for character, so one stray space in a title number turns a reappraisal into a brand-new
master, breaking history. When the key misses *and* the appraisal is a ReAppraisal or Progressive
with `PrevAppraisalId` set, the previous appraisal's master is reused instead. Guards: only those two
appraisal types (`:679`), never a master already claimed by another group in this appraisal, never
across incompatible types, and for land the location must corroborate — a parcel in a different
sub-district must never be folded in, because there is no tool to split it apart afterwards.

## 5. Leasehold — one property, two rows ⭐

**The shape of the data.** A lease agreement is not a separate property in this system. One
`AppraisalProperty` carries both the real-estate detail and the lease contract, because the UI puts
them on tabs of the same page: `Appraisal.AddLeaseAgreementLandProperty()` (`Appraisal.cs:351`)
attaches `LandDetail` + `LeaseAgreementDetail` + `RentalInfo` to a single property. `LSB` gets a
building detail, `LS` land + building, `LSU` a condo detail. Appraisers never key a separate land
row — which is why scanning sibling properties for an underlying used to fail outright.

**What we produce.** Confirmed with AS400 (2026-08-14): two CollateralMaster rows.

```
LSL / LSB / LS / LSU  ──LeaseholdDetail.UnderlyingMasterId──▶  L / LB / U
       (lease)                                                     (RE)
  owns the engagement                                       no engagement, ever
  → HostCollateralId                                        → never reaches AS400
  → flows to AS400                                          → fully populated locally
```

The RE row is the real thing being leased and carries the physical data — owner, area, coordinates,
road and zoning context — written through `UpsertFromLandAppraisal` / `UpsertFromCondoAppraisal`
(`CollateralMaster.cs:598,623`). Those methods touch last-known fields only and never append an
engagement, which is exactly what keeps the row out of the exports (§9).

Its `CollateralType` reflects what the leasehold describes: `LB` when the leasehold property carries
a building, `L` otherwise, `U` for `LSU`. An existing row is only ever upgraded `L → LB`, never
downgraded — the parcel may hold a building this appraisal does not describe.

**Underlying resolution order** (`ResolveUnderlyingMasterAsync`, `:1291`):

1. the property's **own** land detail, then its **own** condo detail — an `LSU` in an appraisal that
   also holds land must hang off *its* condo, not off the neighbouring parcel
2. sibling `L`/`LB`, then sibling `U`
3. any other leasehold property in the appraisal — the only route open to `LSB`, whose
   `BuildingAppraisalDetail` carries no address at all

Before querying the database the resolver matches against the land rows pass 1 just created
(`pass1LandRows`): those are Added-but-unsaved and EF Core queries do not return them, so going
straight to the DB would mint a duplicate and trip `UX_LandDetails_DedupKey_Active` on save.

**In a mixed appraisal** (leasehold alongside plain land) rule §3.1 still applies: land is the
primary and the lease row becomes an alias of it.

## 6. CollateralEngagement

**Exactly one per appraisal** — `UX_CollateralEngagements_Appraisal` is unique on `AppraisalId` alone
(`CollateralEngagementConfiguration.cs:72`). It attaches to the primary master only
(`AppendEngagement`, `:2034`) and holds the appraisal-time facts: value, land area, appraiser,
company, the JSON snapshot of every group, and the AS400 round-trip fields `HostCollateralId`,
`RecordIndicator`, `RecordDate`.

Buildings become `CollateralEngagementBuilding` rows, one per building property — `B`/`LB` and, since
the leasehold work, `LSB`/`LS`. Note they are kept in a separate list from `buildingProperties`
(`:101`): that list also decides whether a land group is typed `L` or `LB`, and a building belonging
to a leasehold must not upgrade an unrelated land master.

**Replay-safe.** `engagementExists` is checked up front (`:67`); on a replay the masters refresh but
no second engagement is appended. Skipping the append rather than letting the unique index reject it
matters, because that catch wraps the whole `SaveChangesAsync` and would roll back legitimate master
updates.

## 7. Failure policy — what kills the appraisal, what does not ⭐

`MissingIdentityKeyException` dead-letters the MassTransit message, and that costs **every** property
in the appraisal its master, not just the offending one. So the two behaviours are chosen
deliberately:

| Situation | Behaviour |
|---|---|
| Land / Condo / Machine missing its identity key | **throw** → dead-letter the whole appraisal (`GetMissingFields`, `:565`) |
| Titles span several existing master groups | **`ConflictException`** → dead-letter without retry; an admin must merge first (`:983`) |
| Lease contract incomplete (ContractNo / Lessor / Lessee / LeaseTermStart) | warn, skip that property only (`MissingLeaseContractFields`, `:1531`) |
| No underlying land/condo resolvable for a leasehold | warn, skip that property only |
| Land alias whose parent is soft-deleted | warn, skip that title only |

Leasehold is deliberately absent from the `ValidateAllProperties` gate. In dev data 14 of 16
leasehold rows have empty contract fields; gating on them would dead-letter those appraisals whole,
taking unrelated land and machinery down with them.

## 8. Block projects (PRJ)

`UpsertProjectAsync` runs as its own branch *before* the per-property loop (`:80`), because a block
appraisal has no `Properties` rows at all — the normal loop is a no-op for it. Its collateral id
grain is the unit, not the master; see the AS400 document.

## 9. What actually gates a row into AS400

**The engagement is the gate, not `IsMaster`.** Both outbound paths start FROM
`CollateralEngagements`, so a master with no engagement cannot appear no matter what its flags say.
The leasehold's RE row proves it: that row *is* `IsMaster = true` and still never exports, because
`AppendEngagement` only ever targets the primary master.

The two interfaces then apply different additional gates — they are not interchangeable:

| | `COLLATERAL_RESULT` (daily) | `REGULATORY` (monthly) |
|---|---|---|
| Source | `CollateralResultQuery.GetApprovedRowsAsync` | `vw_RegulatoryExport` |
| Grain | one row per **appraisal** | one row per **(chain, master)**, latest appraisal of each |
| Engagement required | yes | yes (via the `ChainTip` CTE) |
| `IsMaster = 1` filter | **no such filter** | **yes** — belt and braces, since engagements only attach to IsMaster rows anyway |
| `HostCollateralId` | **required** — `where e.HostCollateralId != null`; no id, no row | **not required** — a missing id renders as zeros (scope widened 2026-08-14 to report everything we hold an appraisal for) |
| Master soft-deleted | excluded (`!m.IsDeleted`) | excluded (`m.IsDeleted = 0`) |
| Already sent | excluded via `CollateralResultLogs` on `AppraisalId` | n/a — the report is a full monthly snapshot |
| Block projects | included like any other type | **excluded explicitly** (`CollateralType <> 'PRJ'`) |
| Redeemed | n/a | excluded when AS400 returned `RecordIndicator = 'R'`; NULL means "not told yet", which is not the same as redeemed |

**Rejected appraisals bypass all of this.** They never receive an engagement — AS400 mints ids at
drawdown, which a rejected appraisal never reaches — so `GetRejectedRowsAsync` reads a separate table,
`PendingCollateralResults where SentAt == null`, and emits an `'R'` row with a blank CCDCID. The
appraisal number is what joins on the host side.

**Practical consequence for the leasehold split:** the RE row stays internal for free, with no extra
filter — but it also means the RE row must never be allowed to become the primary, because that
single change would put it on the wire.

**HostCollateralId lives on the engagement, not the master**, on purpose: a master is
per-physical-collateral-forever and owner-agnostic, so storing the id there would echo the new
owner's id against the old owner's appraisal.

**Leasehold physical attributes must be read through `UnderlyingMasterId`.** The lease row carries
only `LeaseholdDetails`; area, age and coordinates live on the RE row. Both exports therefore
redirect the detail join for leasehold rows — the view via
`COALESCE(lhd_attr.UnderlyingMasterId, m.Id)`, `CollateralResultQuery` via a sub-select on
`m.LeaseholdDetail.UnderlyingMasterId`. Without it every leasehold record went out with zeros in
fields 19/20, contradicting the spec, which counts `LSL`/`LSB`/`LS` as land types and `LSB`/`LS` as
building types (`docs/regulatory/regulatory-field-reference.md:74-75`).

`LSU` is new and absent from that spec text; it is treated as a condo everywhere — area and age from
`CondoDetails`, excluded from the land and building predicates. **Still to confirm with AS400:**
whether the host accepts the literal code `LSU` or expects it mapped to `U` at write time.

---

## Open items

- **The RE row appears in the collateral list UI.** `vw_CollateralMasters` filters only
  `IsDeleted = 0 AND IsMaster = 1`, so a leasehold's underlying row is listed with no appraisal
  history. Not yet decided whether to hide rows that are the underlying of a leasehold.
- **`MarkApprovedByCommittee` skips validation.** `Appraisal.Complete()` calls
  `ValidateCollateralIdentityFields()` (`Appraisal.cs:782`), but the path actually used in production
  — committee approval (`Appraisal.cs:803`) — raises `AppraisalCompletedEvent` without it. Data
  problems therefore surface in the Collateral module rather than at completion. Left alone on
  purpose: throwing there would block committee approval.
