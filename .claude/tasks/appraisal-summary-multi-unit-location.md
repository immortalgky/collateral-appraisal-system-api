# Appraisal summary — multi-unit ที่ตั้งทรัพย์สิน + condo "-" list

Production issue: condo appraisal with 2+ rooms — ที่ตั้งทรัพย์สิน showed one room only, and the
รายการทรัพย์สิน cell had no per-room marker.

## Decisions (user, 2026-10-26)
- Same address tail → room/house numbers once, comma-joined, tail once. Different tails → one segment per tail.
- "-" marker only on the condo list (not land-building).
- Other first-row-only fields (LandOffice, owner, obligation, city plan, GPS, entry/exit) left as is.
- "-"/blank/"0" stay absent (Stated/IsStated). Every unit counts — no "named units only" filter;
  `ห้องชุดเลขที่ ชั้นที่ 1, ชั้นที่ 13` is accepted for rooms keyed as "-".
- Land: every plot kept, even house-less ones with a different tail.
- Condo list text printed as keyed (no extra "-" filtering).

## Todo
- [x] `CollateralLocationSql`: return every U/L unit (B/LSB only when no land), ordered by SequenceNumber
- [x] `ComposeCollateralLocations`: group by tail, `ComposeSameTail` per group
- [x] Condo provider + template: `DetailItems` → `<div>- …</div>` per unit when 2+ units
- [x] Unit tests (`CollateralLocationTests`, new `CondoSummaryTemplateTests`) — 81/81 pass
- [x] Live GET render on a second API (7399, stopped afterwards)
- [ ] User test → code-review → commit/PR

## Code review round 1 (high, all) — triage
Fixed: tower collapse (BuildingNumber in condo group key), B-anchor NOT EXISTS ignores condo-kind land,
stale "first anchor" comments, DetailItems threshold owned by the template only.

### Settled decisions (do not re-raise)
- Construction summary ที่ตั้ง lists all land units of the appraisal, not only CI groups (before: first anchor, also not CI-scoped).
- เขตการปกครอง = first printed unit's sub-district (single-value header field; user: leave first-row fields).
- Address order = appraisal SequenceNumber; condo list order = SequenceInGroup — acceptable.
- No length cap on the address (user wants every unit shown).
- Floors compared as trimmed text; "031" vs "31" not normalised.
- FormatAnchor recomputed per row — perf nit, declined.
- External book (`ExternalBookBuilder`) still first unit only — user: park it, focus on appraisal summary only (no ticket yet).
- User approved (2026-10-26): land row lists every house in its group (own building + building-only B/LSB in the same group).
- `ห้องชุดเลขที่ ชั้นที่ 1, ชั้นที่ 13` for rooms keyed "-" — accepted by user.
- Condo list prints values as keyed (no extra "-" filtering) — user decision.

## Code review round 2 (fix diff only) — all 5 accepted
B house attaches to the group's first land only; B anchor when its own group has no land; same-group
branch only for land anchors; towers dedupe on (room, tower) inside one segment; template test uses real DetailItems contract.

## Code review round 3 (fix diff only)
Fixed: reverted per-group B anchor (B/LSB anchor only when the appraisal has no non-condo land — it has no
address columns of its own); condo-property buildings excluded from the house APPLY; tower dedupe key only for units with a room.
Settled (do not re-raise):
- B/LSB in a land-less group while land exists elsewhere is not listed (no address columns; original rule).
- Which land a group's B house belongs to is unknowable — it attaches to the group's first land row.
- SQL rules have no automated test (would need DB integration test) — verified by read-only runs on local DB.
- Provider `DetailItems = detailParts` line not unit-tested (one assignment; live render verified).
- No CTE refactor of the land-exists subqueries.

## Code review round 4 (fix diff only) — stop condition met
No crash / wrong-number findings. Declined (low / refactor, do not re-raise): roomless "ชั้นที่ N" repeated next to a
named unit on the same floor (cosmetic); BuildingNumber not case/format-normalised; classify by ap.PropertyType
instead of detail tables; first-land subquery per pairing (perf); two copies of "non-condo land" rule.

## Review
- Shared fix in `AppraisalSummaryCommonLoader.cs` reaches condo, land-building, construction, machine
  fallback, internal book cover, approver block. External book (`ExternalBookBuilder`) still first plot only — out of scope.
- Single unit output unchanged (existing tests pass untouched).
- Local DB has no condo appraisal with 2+ real room numbers in one group; that path is covered by unit tests only.
- No new user input or SQL parameters beyond `@AppraisalId`; no secrets.
