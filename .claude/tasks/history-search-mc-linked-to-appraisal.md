# History Search — criteria drive appraisals; MC follows the matched appraisals

## Problem
Today `HistorySearchQueryHandler` filters appraisal (green) pins by all business criteria,
but market-comparable (blue) pins are filtered **only** by radius + date. Several criteria
(AppraisalReportNo, TitleDeedNo, CollateralType, CustomerName, LandArea, BuildingType, address)
have no equivalent column on `MarketComparable`, so the blue list ignores them and returns
everything in the date window.

## New behaviour (confirmed with user)
- **Search criteria apply to the appraisal (application) only.**
- **Attribute mode (no centre):** blue pins = the comparables **linked** (via
  `appraisal.AppraisalComparables`) to the appraisals that match the criteria.
  - External users: their **own-company** comparables linked to matched appraisals
    (criteria-driven, scoped by `CreatedByCompanyId`).
- **Radius mode (centre + radius):** unchanged — blue pins = **all** MCs within the radius
  (geographic), date-filtered on `mc.InfoDateTime`. Externals scoped to own company.

Green (appraisal) pins are unchanged in both modes. Contract unchanged → **no frontend work**.

## Plan
- [ ] Extract the appraisal date + business + address filters into a reusable
      `AppendAppraisalFilters(ref sql, p, query, dateFrom, dateTo)` helper that references
      aliases `a` (Appraisals) and `al` (vw_AppraisalList). Move the date-window filters
      into it (so the linked-MC EXISTS gets them too).
- [ ] `QueryAppraisalPinsAsync`: replace the inline date+business+address block with the helper.
      Keep the radius (rep CROSS APPLY) and ordering logic as-is.
- [ ] `QueryMarketComparablePinsAsync`: branch on `center`:
      - centre given → current geographic/radius branch (mc.InfoDateTime date window).
      - no centre → constrain to MCs `EXISTS` in `AppraisalComparables` joined to appraisals
        matching the criteria (helper inside the EXISTS, on the appraisal — NOT on mc.InfoDateTime).
      - external `CreatedByCompanyId` scope applies in both branches (unchanged).
- [ ] Build check; run `reviewer` agent before finishing.

## Notes / safety
- Each query method keeps its own `DynamicParameters`; no shared-param mutation across the
  parallel internal branches.
- No new migration / view changes. `AppraisalComparables(MarketComparableId, AppraisalId)`
  already exists (used by the MC SELECT-clause subqueries today).
- Do NOT apply DB changes — none required here anyway.

## Review
- Implemented all three steps; Appraisal module builds with 0 errors.
- `reviewer` agent: **PASS**. Verified alias scoping (helper `a`/`al` bind to the MC EXISTS
  subquery scope; nested EXISTS aliases unique; SELECT-list correlated subqueries are separate
  scopes — no collision), no duplicate `DynamicParameters.Add` on any path, SQL-injection safe
  (no new user-input interpolation), external `CreatedByCompanyId` scoping preserved in both MC
  modes, no regression in radius mode.
- Reviewer note (not a defect, pre-existing): external visibility depends on
  `CurrentUserService.IsExternal => CompanyId.HasValue`. Safe today. Optional future hardening:
  guard `IsExternal && CompanyId is null`.
- No frontend / DB / migration changes.
