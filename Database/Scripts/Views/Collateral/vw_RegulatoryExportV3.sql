-- CAS-AS400-Regulatory export view, VERSION 3.
--
-- ONE ROW PER COLLATERAL THE BANK HOLDS, carrying the date and value of the FIRST time CAS ever
-- appraised it.
--
-- ── Why a third version ────────────────────────────────────────────────────────────────────────
-- v1 and v2 both start from an APPRAISAL and try to work out which collateral it stands for — v1 via
-- CollateralMaster, v2 via the PrevAppraisalId chain. Both are guesses, and both were wrong in ways
-- that took weeks to characterise: v1 lost 6,699 appraisals that never got a master, v2 had to invent
-- branch-point rules to stop unrelated parcels collapsing into one row, and neither could report a
-- block project's units at all.
--
-- The AS400 feed answers the question directly. AS400_COLLAT (byte-identical to the COLLATLINK file
-- we already ingest) is ONE ROW PER COLLATERAL with the appraisal number attached — 32,662 rows,
-- 32,662 distinct collateral ids, no duplicates. Starting from it removes the guessing entirely:
--
--   * no chain-tip selection — AS400 says which collateral exists, we do not choose
--   * no branch-point rule — two parcels that share an ancestor are simply two rows
--   * block projects work — the per-unit key the project export was waiting on IS the collateral id
--
-- What the report is FOR (confirmed with the business 2026-08-20): for each collateral the bank
-- holds, the value and date of its FIRST appraisal. The bank looks the CURRENT value up in AS400
-- itself using the collateral id; it does not want ours.
--
-- ── Grain and filters ──────────────────────────────────────────────────────────────────────────
--   collateral.HostCollateralLinks, one row per AS400 collateral id, filtered to
--     MasterTitle stated  — 'Y' or 'N', both reported. A BLANK is not: AS400 truncates trailing
--                           spaces, so 1,516 rows of the 2026-08-03 file stop short of pos 132 and
--                           state nothing; only 37 of those are still held.
--     IsRedeemed = 0      — released collateral is not reported.
--   On the 2026-08-03 feed that is 24,574 of 32,662 rows.
--
-- ── Walking back to the first appraisal ────────────────────────────────────────────────────────
-- The appraisal AS400 names is usually, but NOT always, the first one: 91.3% of held master-title
-- rows are already the head of their chain, and for the rest the first appraisal's value genuinely
-- differs — 1,093 rows (4.9%) would report the wrong origination value without the walk. So
-- PrevAppraisalId is still walked, but ONLY to find the oldest ancestor. It never decides which row
-- exists.
--
-- The walk follows PrevAppraisalId wherever it leads and does NOT stop at a branch point.
--
-- v2 had to stop there: the walk DECIDED the grain, so crossing a branch merged unrelated parcels
-- into one row. Here AS400 fixes the grain, and the walk only prices the collateral it was seeded
-- from — there is nothing to merge. Stopping instead threw away real history: collateral 104428 sits
-- on deed 12380, its appraisal 67A00231 continues 66A00583 which values that SAME deed at 30,000,000,
-- and the only reason the walk halted was a sibling (67A02401) on an unrelated deed pointing at the
-- same predecessor. PrevAppraisalId is the user's own assertion that one appraisal follows another;
-- a sibling's existence is not evidence against it.
--
-- ── Building type now comes from AS400 ─────────────────────────────────────────────────────────
-- The feed carries PropertyType (PCO/PSH/PTH/…) AND its Thai description, so the export reports
-- AS400's own taxonomy instead of CAS's BuildingTypeCode — which is "99 อื่นๆ" for 85-100% of rows in
-- every bucket and never mapped onto AS400's codes. This closes the long-standing building-type gap
-- with no code table to request.
--
-- OPTION (MAXRECURSION 0) cannot live inside a view — the caller adds it. See RegulatoryExportV3Query.
CREATE OR ALTER VIEW collateral.vw_RegulatoryExportV3
AS
WITH

-- ── The row set: what AS400 says the bank holds ────────────────────────────────────────────────
Src AS (
    SELECT
        h.HostCollateralId,
        h.AppraisalNumber,
        -- AS400 prefixes a block project's appraisal number with 'B'; CAS stores it without. All 107
        -- prefixed numbers on the 2026-08-03 feed match a CAS appraisal once the letter is removed —
        -- no exceptions — and the bank's own file carries them unprefixed too (not one of its 63,095
        -- rows starts with 'B'). Not every block is prefixed, so this normalises rather than detects.
        CASE WHEN LEFT(h.AppraisalNumber, 1) = 'B'
             THEN SUBSTRING(h.AppraisalNumber, 2, LEN(h.AppraisalNumber))
             ELSE h.AppraisalNumber
        END AS CasAppraisalNumber,
        -- For a block-project unit AS400 writes the name as "CONDO.<key> <deeds>", where <key>
        -- identifies the unit. Everything up to the first space is that key; NULL for anything that
        -- is not a CONDO row. See ProjectUnitValue for what it is matched against.
        CASE WHEN h.CollateralName LIKE 'CONDO.%' THEN
            CASE WHEN CHARINDEX(' ', SUBSTRING(h.CollateralName, 7, 200)) > 0
                 THEN LEFT(SUBSTRING(h.CollateralName, 7, 200),
                           CHARINDEX(' ', SUBSTRING(h.CollateralName, 7, 200)) - 1)
                 ELSE SUBSTRING(h.CollateralName, 7, 200)
            END
        END AS UnitToken,
        h.PropertyType,
        h.PropertyTypeDesc
    FROM collateral.HostCollateralLinks h
    -- Both 'Y' and 'N' are reported; only a row where the feed never stated the flag is left out.
    -- The bank's own file carries collateral flagged 'N' (collateral 59305 appears three times in the
    -- 2026-08-02 file), so treating 'N' as unreportable dropped 114 collateral the bank does report.
    -- A blank is different: the row stopped short of pos 132 and AS400 said nothing at all.
    WHERE h.MasterTitle IS NOT NULL
      AND h.IsRedeemed = 0
),

-- The appraisal AS400 named. A collateral whose appraisal number is not in appraisal.Appraisals
-- drops out here — 1,930 rows on the 2026-08-03 feed. SOURCE 2 below recovers the 1,177 of them that
-- are the legacy "99" series; the rest (604 block-project 'B' numbers, 146 in a numbering CAS does
-- not use) are questions for AS400, not something this view can resolve.
Anchor AS (
    SELECT s.HostCollateralId, s.PropertyType, s.PropertyTypeDesc,
           a.Id AS AppraisalId, a.AppraisalNumber, a.AppraisalType, a.RequestId, a.PrevAppraisalId,
           -- A block-project appraisal holds no AppraisalProperties at all, so PropMix has nothing to
           -- read and the collateral type would fall through to "bare land". The project's own type is
           -- the only thing in CAS that says whether its units are condos or houses. It carries the
           -- same codes the export already uses — 'U' or 'LB', nothing else.
           pr.ProjectType
    FROM Src s
    JOIN appraisal.Appraisals a
        ON  a.AppraisalNumber = s.CasAppraisalNumber
        AND a.IsDeleted       = 0
    LEFT JOIN appraisal.Projects pr ON pr.AppraisalId = a.Id
),

-- ── Walk each anchor back through its predecessors ─────────────────────────────────────────────
-- Seeded from the anchors only, not from every appraisal in the database — the row set is fixed by
-- AS400, so there is nothing to gain from walking the rest.
--
-- The cycle guard is the Path CHARINDEX test, NOT a depth limit. A depth predicate would stop the
-- recursion before MAXRECURSION could fire and silently return the Nth ancestor as if it were the
-- first.
Walk AS (
    SELECT
        an.AppraisalId,
        an.AppraisalId      AS AncestorId,
        an.PrevAppraisalId,
        0                   AS Depth,
        CAST('|' + CAST(an.AppraisalId AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM Anchor an

    UNION ALL

    SELECT
        w.AppraisalId,
        p.Id,
        p.PrevAppraisalId,
        w.Depth + 1,
        CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
    FROM Walk w
    JOIN appraisal.Appraisals p
        ON  p.Id        = w.PrevAppraisalId
        AND p.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
      -- A block project is not a step in one collateral's history: many unrelated units point at the
      -- same project, and a project can point back at a unit it absorbed. Dead end both ways.
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = p.Id)
      AND NOT EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = w.AncestorId)
),

Val AS (
    SELECT v.AppraisalId, v.ValuationDate, v.AppraisedValue
    FROM appraisal.ValuationAnalyses v
),

-- The oldest ancestor that actually has a valuation — that is the origination. Depth breaks ties so
-- the result cannot change between runs when two rounds share a date.
Earliest AS (
    SELECT AppraisalId, AncestorId, ValuationDate, AppraisedValue
    FROM (
        SELECT w.AppraisalId, w.AncestorId, v.ValuationDate, v.AppraisedValue,
               ROW_NUMBER() OVER (PARTITION BY w.AppraisalId
                                  ORDER BY v.ValuationDate ASC, w.Depth DESC) AS rn
        FROM Walk w
        JOIN Val v ON v.AppraisalId = w.AncestorId
    ) z
    WHERE rn = 1
),

-- ── Construction status ────────────────────────────────────────────────────────────────────────
-- Mirrors IConstructionCurrentValueService.CiAggregateSql exactly, so the screen and this file
-- cannot disagree. A summary inspection carries its own percentage; a full-detail one is the sum of
-- its work rows.
Ci AS (
    SELECT
        ap.AppraisalId,
        ISNULL(SUM(ci.TotalValue), 0) AS TotalValue,
        ISNULL(SUM(
            CASE WHEN ci.IsFullDetail = 0
                 THEN ci.TotalValue * ISNULL(ci.SummaryCurrentProgressPct, 0) / 100.0
                 ELSE ISNULL(wd.CurrentSum, 0)
            END), 0) AS CurrentValue
    FROM appraisal.ConstructionInspections ci
    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
    LEFT JOIN (
        SELECT ConstructionInspectionId, SUM(CurrentPropertyValue) AS CurrentSum
        FROM appraisal.ConstructionWorkDetails
        GROUP BY ConstructionInspectionId
    ) wd ON wd.ConstructionInspectionId = ci.Id
    GROUP BY ap.AppraisalId
),

-- ── What each appraisal is made of ─────────────────────────────────────────────────────────────
PropMix AS (
    SELECT
        p.AppraisalId,
        MAX(CASE WHEN p.PropertyType IN ('L', 'LB')  THEN 1 ELSE 0 END) AS HasLand,
        -- 'LB' is a single property that IS land-and-building: it carries both a
        -- LandAppraisalDetails and a BuildingAppraisalDetails row. Treating a separate 'B' row as the
        -- only building signal typed 34,077 land-and-building records as bare land.
        MAX(CASE WHEN p.PropertyType IN ('B', 'LB')  THEN 1 ELSE 0 END) AS HasBuilding,
        MAX(CASE WHEN p.PropertyType = 'U'           THEN 1 ELSE 0 END) AS HasCondo,
        MAX(CASE WHEN p.PropertyType = 'LSL'         THEN 1 ELSE 0 END) AS HasLeaseLand,
        MAX(CASE WHEN p.PropertyType = 'LSB'         THEN 1 ELSE 0 END) AS HasLeaseBuilding,
        MAX(CASE WHEN p.PropertyType = 'LS'          THEN 1 ELSE 0 END) AS HasLeaseBoth,
        MAX(CASE WHEN p.PropertyType = 'LSU'         THEN 1 ELSE 0 END) AS HasLeaseCondo,
        MAX(CASE WHEN p.PropertyType IN ('L','LB','U','LSL','LSB','LS','LSU','B') THEN 1 ELSE 0 END) AS HasRealEstate
    FROM appraisal.AppraisalProperties p
    GROUP BY p.AppraisalId
),

LandAgg AS (
    SELECT
        p.AppraisalId,
        SUM(ISNULL(t.AreaRai, 0) * 400 + ISNULL(t.AreaNgan, 0) * 100 + ISNULL(t.AreaSquareWa, 0)) AS LandAreaSqWa
    FROM appraisal.AppraisalProperties p
    JOIN appraisal.LandAppraisalDetails d ON d.AppraisalPropertyId = p.Id
    JOIN appraisal.LandTitles t           ON t.LandAppraisalDetailId = d.Id
    WHERE p.PropertyType IN ('L', 'LB', 'LSL', 'LS')
    GROUP BY p.AppraisalId
),

-- Buildings combined: the OLDEST age and the TOTAL area across every building, matching the rule the
-- AS400 result interface uses. A single building cannot speak for a plot that holds several.
BuildingAgg AS (
    SELECT
        p.AppraisalId,
        MAX(b.BuildingAge)        AS MaxBuildingAge,
        SUM(b.TotalBuildingArea)  AS TotalBuildingArea
    FROM appraisal.AppraisalProperties p
    JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = p.Id
    WHERE p.PropertyType IN ('B', 'LB', 'LSB', 'LS')
    GROUP BY p.AppraisalId
),

RepBuilding AS (
    SELECT AppraisalId, NumberOfFloors
    FROM (
        SELECT p.AppraisalId, b.NumberOfFloors,
               ROW_NUMBER() OVER (PARTITION BY p.AppraisalId ORDER BY p.SequenceNumber) AS rn
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('B', 'LB', 'LSB', 'LS')
    ) z
    WHERE rn = 1
),

CondoAgg AS (
    SELECT AppraisalId, UsableArea, BuildingAge
    FROM (
        SELECT p.AppraisalId, c.UsableArea, c.BuildingAge,
               ROW_NUMBER() OVER (PARTITION BY p.AppraisalId ORDER BY p.SequenceNumber) AS rn
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.CondoAppraisalDetails c ON c.AppraisalPropertyId = p.Id
        WHERE p.PropertyType IN ('U', 'LSU')
    ) z
    WHERE rn = 1
),

-- ── Which appraisal the physical characteristics are read from ─────────────────────────────────
-- The anchor when it has property rows of its own; otherwise the nearest ancestor that does. A
-- Progressive round records construction progress on top of an earlier survey and often carries no
-- AppraisalProperties at all.
--
-- This MUST be a CTE joined once, never an OUTER APPLY correlated on the anchor. SQL Server does not
-- materialise a CTE: an APPLY over Walk re-runs the whole recursion once per output row, which took
-- v2 from 7 seconds to 108.
PropSource AS (
    SELECT AppraisalId, AncestorId AS PropSourceAppraisalId
    FROM (
        SELECT w.AppraisalId, w.AncestorId,
               ROW_NUMBER() OVER (PARTITION BY w.AppraisalId ORDER BY w.Depth ASC) AS rn
        FROM Walk w
        WHERE EXISTS (SELECT 1 FROM appraisal.AppraisalProperties p
                      WHERE p.AppraisalId = w.AncestorId)
    ) z
    WHERE rn = 1
),

-- ── Per-unit value for block-project collateral ───────────────────────────────────────────────
-- A block project's own appraisal is a PreAppraisal of the whole development: it carries no
-- AppraisalProperties and an AppraisedValue of 0. Reporting that as the collateral's value tells the
-- regulator the unit is worth nothing. The per-unit figure lives with the APPRAISAL that surveyed the
-- units — appraisal.Projects → appraisal.ProjectUnits → appraisal.ProjectUnitPrices.
--
-- ⚠ NOT collateral.ProjectUnits. That table is keyed by CollateralMasterId, and a project master is
-- shared by every appraisal of that development, so reaching it through CollateralEngagements returns
-- units belonging to a DIFFERENT appraisal. 65A04972 (PreAppraisal, 2022, no units of its own) was
-- getting 13,604,000 from 67A06099's 2024 re-survey that way. It also reintroduces the dependency on
-- CollateralMaster that this whole view exists to remove.
--
-- WHICH APPRAISAL'S UNIT. The same rule as everything else here: walk PrevAppraisalId back from the
-- appraisal AS400 named, and take the unit from the OLDEST appraisal in that history that surveyed
-- it — that is the origination. The anchor's own units win for the current value.
ProjectWalk AS (
    SELECT
        an.AppraisalId,
        an.HostCollateralId,
        s.UnitToken,
        an.AppraisalId AS AncestorId,
        an.PrevAppraisalId,
        0              AS Depth,
        CAST('|' + CAST(an.AppraisalId AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM Anchor an
    JOIN Src s ON s.HostCollateralId = an.HostCollateralId
    WHERE s.UnitToken IS NOT NULL

    UNION ALL

    -- Unlike the main Walk this DOES step through project appraisals. There it is a dead end because
    -- unrelated units point at a shared project and merging them would confuse two collateral; here
    -- the unit token pins one specific unit, so following the project's own history is exactly right.
    SELECT w.AppraisalId, w.HostCollateralId, w.UnitToken, p.Id, p.PrevAppraisalId, w.Depth + 1,
           CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
    FROM ProjectWalk w
    JOIN appraisal.Appraisals p ON p.Id = w.PrevAppraisalId AND p.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
),

-- Every appraisal in that history that actually priced this unit.
ProjectUnitHit AS (
    SELECT
        w.HostCollateralId,
        w.Depth,
        v.ValuationDate,
        pup.TotalAppraisalValueRounded AS UnitValue,
        -- Registration first, room number second: migrated projects recorded the unit under
        -- RoomNumber, newly appraised ones record CondoRegistrationNumber. Ordering rather than
        -- branching keeps one rule working as the data shifts.
        CASE WHEN pu.CondoRegistrationNumber = w.UnitToken THEN 0 ELSE 1 END AS KeyRank
    FROM ProjectWalk w
    JOIN appraisal.Projects pr      ON pr.AppraisalId = w.AncestorId
    JOIN appraisal.ProjectUnits pu  ON pu.ProjectId   = pr.Id
    JOIN appraisal.ProjectUnitPrices pup ON pup.ProjectUnitId = pu.Id
    LEFT JOIN Val v ON v.AppraisalId = w.AncestorId
    WHERE ISNULL(pup.TotalAppraisalValueRounded, 0) > 0
      AND (pu.CondoRegistrationNumber = w.UnitToken OR pu.RoomNumber = w.UnitToken)
),

-- Collateral that IS a project unit: AS400 named a unit (CONDO.<key>) and the appraisal history runs
-- through a block project. For these the unit table is the ONLY acceptable source of value — the
-- appraisal-level figure belongs to whatever that appraisal covered, not to this one unit. When the
-- unit cannot be found the row reports 0 rather than borrowing that figure.
--
-- 69A03063 is the case that forced this: a standalone condo appraisal worth 4,180,000 whose
-- PrevAppraisalId runs back to project 65A03510, which holds no units at all. Reporting 4,180,000 as
-- unit 99/1832's value was a guess dressed up as data.
ProjectUnitRow AS (
    SELECT DISTINCT w.HostCollateralId
    FROM ProjectWalk w
    WHERE EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = w.AncestorId)
),

ProjectUnitValue AS (
    SELECT
        HostCollateralId,
        MIN(CASE WHEN rnEarliest = 1 THEN UnitValue END) AS EarliestUnitValue,
        MIN(CASE WHEN rnLatest   = 1 THEN UnitValue END) AS LatestUnitValue
    FROM (
        SELECT h.*,
               -- Oldest survey of this unit = its origination.
               ROW_NUMBER() OVER (PARTITION BY h.HostCollateralId
                                  ORDER BY h.ValuationDate ASC, h.Depth DESC, h.KeyRank) AS rnEarliest,
               -- Nearest to the appraisal AS400 named = its current value.
               ROW_NUMBER() OVER (PARTITION BY h.HostCollateralId
                                  ORDER BY h.Depth ASC, h.ValuationDate DESC, h.KeyRank) AS rnLatest
        FROM ProjectUnitHit h
    ) z
    GROUP BY HostCollateralId
),

-- ── The legacy "99" series ─────────────────────────────────────────────────────────────────────
-- Collateral the bank already held when CAS went live. It was never appraised here, so no row exists
-- in appraisal.Appraisals and SOURCE 1 cannot see it — but the bank supplies its regulatory data
-- separately in appraisal.AS400ReportListing, and the AS400 feed still reports the collateral with a
-- '99…' appraisal number. 1,177 held master-title collateral on the 2026-08-03 feed.
--
-- Keyed by CollateralID, not ApplicationId: the grain of this view is the collateral, and matching on
-- the collateral id is what makes the two sources line up on the same thing.
--
-- Aggregated before it is joined. The listing is NOT unique on CollateralID — one collateral has two
-- rows on U3 — and joining the rows themselves would emit that collateral twice. MIN(ValuationDate)
-- is the origination by definition; MIN over the price with it keeps the pair deterministic rather
-- than letting the two columns come from different rows.
LegacyByCollateral AS (
    SELECT
        CAST(CAST(l.CollateralID AS bigint) AS varchar(19)) AS HostCollateralId,
        MIN(l.ValuationDate)                                AS ValuationDate,
        MIN(l.ValuationPriceInBaht)                         AS ValuationPriceInBaht
    FROM appraisal.AS400ReportListing l
    WHERE l.CollateralID IS NOT NULL
    GROUP BY CAST(CAST(l.CollateralID AS bigint) AS varchar(19))
)

-- ═══════════════════════════ SOURCE 1 — collateral CAS has appraised ═══════════════════════════
SELECT
    an.AppraisalNumber                                           AS LatestAppraisalNumber,

    -- Derived from the property mix of whichever appraisal actually described the property.
    CASE
        WHEN pm.HasLeaseBoth     = 1 THEN 'LS'
        WHEN pm.HasLeaseBuilding = 1 THEN 'LSB'
        -- Leased land with a building on it is LS, not LSL: the writer sends 'L' — "no structure" —
        -- for L and LSL alike, so an LSL row carrying buildings would tell the regulator the plot is
        -- empty.
        WHEN pm.HasLeaseLand     = 1 AND pm.HasBuilding = 1 THEN 'LS'
        WHEN pm.HasLeaseLand     = 1 THEN 'LSL'
        WHEN pm.HasLeaseCondo    = 1 THEN 'LSU'
        WHEN pm.HasLand = 1 AND pm.HasBuilding = 1 THEN 'LB'
        WHEN pm.HasLand = 1 THEN 'L'
        WHEN pm.HasCondo = 1 THEN 'U'
        WHEN pm.HasBuilding = 1 THEN 'LB'
        -- Nothing in CAS describes the property, which means a block-project appraisal: it is a
        -- PreAppraisal of the whole development and carries no AppraisalProperties. Falling through to
        -- 'L' reported all 1,607 project rows as bare land — 1,552 of them condo units — and the writer
        -- turned that into field 5 = 'L', "no structure". The bank's own file sends 'N' for every one
        -- of the 696 it holds, never 'L'.
        --
        -- ProjectType is CAS's own answer and agrees with what AS400 says in the feed on all 1,607
        -- rows (U↔PCO, LB↔PSH/PTH/PWH), so no code mapping is needed and the undocumented AS400 codes
        -- POT/PHR never have to be guessed at.
        WHEN an.ProjectType IN ('U', 'LB') THEN an.ProjectType
        ELSE 'L'
    END                                                          AS CollateralType,

    an.HostCollateralId,
    an.AppraisalType                                             AS LatestAppraisalType,

    CASE WHEN ISNULL(ci.TotalValue, 0) > 0 AND ci.CurrentValue < ci.TotalValue THEN CAST(1 AS bit)
         ELSE CAST(0 AS bit) END                                 AS IsUnderConstruction,

    -- Not real estate → 0; bare land → 0; anything with a structure → 100 when complete, else the
    -- progress percentage.
    CASE
        WHEN pm.HasLand = 1 AND pm.HasBuilding = 0 AND pm.HasCondo = 0
             AND pm.HasLeaseBuilding = 0 AND pm.HasLeaseBoth = 0                     THEN 0
        WHEN pm.HasLeaseLand = 1 AND pm.HasLeaseBuilding = 0 AND pm.HasLeaseBoth = 0
             AND pm.HasBuilding = 0                                                  THEN 0
        WHEN ISNULL(ci.TotalValue, 0) = 0                                            THEN 100
        WHEN ci.CurrentValue >= ci.TotalValue                                        THEN 100
        ELSE CAST(ci.CurrentValue / ci.TotalValue * 100 AS decimal(5, 2))
    END                                                          AS ConstructionProgressPercent,

    -- A project unit's own appraised value beats the project appraisal's 0. Everything else keeps
    -- reading from the appraisal.
    CASE WHEN puv.LatestUnitValue IS NOT NULL         THEN puv.LatestUnitValue
         WHEN pur.HostCollateralId IS NOT NULL       THEN 0
         ELSE av.AppraisedValue END                              AS LatestAppraisalValue,

    -- The origination value, from whichever source holds the older record. AS400 valued a lot of this
    -- collateral before CAS existed, and those valuations live in appraisal.AS400ReportListing under a
    -- '99…' number that is NOT in appraisal.Appraisals — so the PrevAppraisalId walk cannot reach them
    -- however far back it goes. Without this test, 122 collateral report the first CAS appraisal as
    -- their origination: 67A00393 would say 2024-01-26 / 5,750,000 when the bank first valued it on
    -- 2008-11-27 at 4,670,000.
    --
    -- The date and the value must come from the SAME record. Taking MIN of each independently would
    -- pair one source's date with the other's price.
    -- Same for the origination value. A unit has one appraised figure, not a history — the project
    -- was appraised once and the unit inherits that date — so the same number stands for both ends.
    CASE WHEN puv.EarliestUnitValue IS NOT NULL THEN puv.EarliestUnitValue
         -- A project unit with no unit data reports 0; see ProjectUnitRow.
         WHEN pur.HostCollateralId IS NOT NULL THEN 0
         WHEN lgc.ValuationDate IS NOT NULL AND lgc.ValuationDate < e.ValuationDate
              THEN lgc.ValuationPriceInBaht
         ELSE e.AppraisedValue END                               AS EarliestAppraisalValue,

    CASE WHEN ISNULL(ci.TotalValue, 0) > 0 AND ci.CurrentValue < ci.TotalValue
         THEN ci.CurrentValue ELSE NULL END                      AS CurrentValue,

    rd.TotalSellingPrice                                         AS SellingPrice,

    CASE WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1
         THEN CAST(rb.NumberOfFloors AS int) ELSE NULL END       AS NumberOfFloors,

    CASE
        WHEN pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1 THEN ba.MaxBuildingAge
        WHEN pm.HasCondo = 1 OR pm.HasLeaseCondo = 1                              THEN ca.BuildingAge
        ELSE NULL
    END                                                          AS BuildingAge,

    av.ValuationDate                                             AS LatestAppraisalDate,


    -- Only meaningful when the appraisal AS400 named is itself a construction round. v2 searched a
    -- chain for the newest Progressive; here the anchor IS the appraisal AS400 considers current, so
    -- looking past it would report a date the bank never asked about.
    CASE WHEN an.AppraisalType = 'Progressive' THEN av.ValuationDate END
                                                                 AS LatestProgressiveAppraisalDate,

    CASE WHEN lgc.ValuationDate IS NOT NULL AND lgc.ValuationDate < e.ValuationDate
         THEN lgc.ValuationDate ELSE e.ValuationDate END      AS EarliestAppraisalDate,

    NULL                                                         AS LatestAppraisalCompanyId,

    -- DOPA 6-digit sub-district. Administrative address, so it must come from a DOPA-mastered source:
    -- request.RequestDetails, whose picker uses the DOPA master. NOT the deed address, which is
    -- mastered by parameter.TitleSubDistricts and has diverged from DOPA.
    CASE WHEN pm.HasRealEstate = 1
         THEN (SELECT dsd.Code FROM parameter.DopaSubDistricts dsd
               WHERE dsd.Code = LTRIM(RTRIM(rd.SubDistrict)))
         ELSE NULL END                                           AS DopaCode,

    -- Two-sided range guards. A value outside [0, 99999.99] overflows the fixed-width field, and a
    -- NEGATIVE one is as fatal as an oversized one because the writer multiplies by 100 and the minus
    -- sign consumes a character. One bad row used to abort the entire file.
    CASE WHEN la.LandAreaSqWa NOT BETWEEN 0 AND 99999.99 THEN NULL
         ELSE la.LandAreaSqWa END                                AS LandAreaSqWa,

    CASE
        WHEN (pm.HasBuilding = 1 OR pm.HasLeaseBuilding = 1 OR pm.HasLeaseBoth = 1)
             AND ba.TotalBuildingArea BETWEEN 0 AND 99999.99 THEN ba.TotalBuildingArea
        WHEN (pm.HasCondo = 1 OR pm.HasLeaseCondo = 1)
             AND ca.UsableArea BETWEEN 0 AND 99999.99        THEN ca.UsableArea
        ELSE NULL
    END                                                          AS BuildingArea,

    -- AS400's own taxonomy, straight from the feed, description included. CAS's BuildingTypeCode is
    -- deliberately not used: it is "99 อื่นๆ" for 85-100% of rows in every bucket and has no mapping
    -- onto these codes.
    an.PropertyType                                              AS BuildingTypeCode,
    an.PropertyTypeDesc                                          AS BuildingTypeDescription

FROM Anchor an
LEFT JOIN Val av           ON av.AppraisalId = an.AppraisalId
LEFT JOIN Earliest e       ON e.AppraisalId  = an.AppraisalId
LEFT JOIN Ci ci            ON ci.AppraisalId = an.AppraisalId
LEFT JOIN PropSource ps    ON ps.AppraisalId = an.AppraisalId
LEFT JOIN PropMix pm       ON pm.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN LandAgg la       ON la.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN BuildingAgg ba   ON ba.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN RepBuilding rb   ON rb.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN CondoAgg ca      ON ca.AppraisalId = ps.PropSourceAppraisalId
LEFT JOIN request.RequestDetails rd ON rd.RequestId = an.RequestId
-- The legacy listing for THIS collateral, if AS400 valued it before CAS existed. Joined on the
-- collateral id, not the appraisal number: the '99…' number that owns the listing row is a
-- different number from the one the feed reports today.
LEFT JOIN LegacyByCollateral lgc ON lgc.HostCollateralId = an.HostCollateralId
LEFT JOIN ProjectUnitValue puv     ON puv.HostCollateralId = an.HostCollateralId
LEFT JOIN ProjectUnitRow pur       ON pur.HostCollateralId = an.HostCollateralId
-- A collateral with no valuation anywhere in its history has no origination value to report, which is
-- the one thing this file exists to carry.
WHERE e.ValuationDate IS NOT NULL

UNION ALL

-- ═══════════════════════════ SOURCE 2 — the legacy "99" series ═══════════════════════════
-- Reported straight from the bank's own migrated data. Everything CAS would normally derive from an
-- appraisal is absent by definition — there is no appraisal — so only the fields the listing actually
-- carries are populated and the rest stay NULL, exactly as v1 and v2 emit them for this series.
SELECT
    s.CasAppraisalNumber                                         AS LatestAppraisalNumber,
    'UNK'                                                        AS CollateralType,
    s.HostCollateralId,
    NULL                                                         AS LatestAppraisalType,
    CAST(0 AS bit)                                               AS IsUnderConstruction,
    -- The bank's own file sends N + 100.00% for this whole series.
    CAST(100 AS decimal(5, 2))                                   AS ConstructionProgressPercent,
    lg.ValuationPriceInBaht                                      AS LatestAppraisalValue,
    -- The listing IS the origination: this collateral has no earlier record anywhere.
    lg.ValuationPriceInBaht                                      AS EarliestAppraisalValue,
    NULL                                                         AS CurrentValue,
    NULL                                                         AS SellingPrice,
    NULL                                                         AS NumberOfFloors,
    NULL                                                         AS BuildingAge,
    lg.ValuationDate                                             AS LatestAppraisalDate,
    NULL                                                         AS LatestProgressiveAppraisalDate,
    lg.ValuationDate                                             AS EarliestAppraisalDate,
    NULL                                                         AS LatestAppraisalCompanyId,
    NULL                                                         AS DopaCode,
    NULL                                                         AS LandAreaSqWa,
    NULL                                                         AS BuildingArea,
    -- AS400 supplies the type for these too, so the series is no longer typeless in the file.
    s.PropertyType                                               AS BuildingTypeCode,
    s.PropertyTypeDesc                                           AS BuildingTypeDescription

FROM Src s
JOIN LegacyByCollateral lg ON lg.HostCollateralId = s.HostCollateralId
-- Only where SOURCE 1 could not report it. A collateral that CAS has appraised is reported from the
-- appraisal; emitting the listing row as well would double-count one physical collateral.
WHERE NOT EXISTS (
        SELECT 1 FROM appraisal.Appraisals a
        WHERE a.AppraisalNumber = s.CasAppraisalNumber AND a.IsDeleted = 0)
  AND lg.ValuationDate IS NOT NULL;
